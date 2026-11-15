import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactElement,
  type ReactNode
} from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { apiOrigin } from "../services/apiBaseUrl";
import { useOptionalAuth } from "./AuthContext";

export interface LiveSignal {
  kind: string;
  key: string;
  actorUserId?: number | null;
}

export type LiveStatus = "connected" | "connecting" | "offline";

export interface LiveConnection {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(methodName: string, handler: (signal: LiveSignal) => void): void;
  off(methodName: string, handler: (signal: LiveSignal) => void): void;
  invoke(methodName: string, ...args: unknown[]): Promise<unknown>;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
  onclose(callback: (error?: Error) => void): void;
}

export interface LiveContextValue {
  status: LiveStatus;
  subscribe(kind: string, key: string | null, fn: (s: LiveSignal) => void): () => void;
  watchRecord(type: string, id: number): () => void;
}

interface SubscriptionEntry {
  kind: string;
  key: string | null;
  fn: (s: LiveSignal) => void;
}

interface WatchedRecordEntry {
  type: string;
  id: number;
  count: number;
}

const RECONNECT_RETRY_MS = 30_000;

const defaultLiveContext: LiveContextValue = {
  status: "offline",
  subscribe: () => () => undefined,
  watchRecord: () => () => undefined
};

const LiveContext = createContext<LiveContextValue>(defaultLiveContext);
const LiveReconnectContext = createContext<(fn: () => void) => () => void>(() => () => undefined);

function createHubConnection(getToken: () => string): LiveConnection {
  return new HubConnectionBuilder()
    .withUrl(`${apiOrigin}/hubs/live`, {
      accessTokenFactory: getToken
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export interface LiveProviderProps {
  children: ReactNode;
  connection?: LiveConnection;
}

export function LiveProvider({ children, connection: connectionProp }: LiveProviderProps): ReactElement {
  const auth = useOptionalAuth();
  const token = auth ? auth.token : (connectionProp ? "test-token" : null);
  const userId = auth ? auth.userId : null;
  const isAuthenticated = Boolean(token);

  const [status, setStatus] = useState<LiveStatus>(isAuthenticated ? "connecting" : "offline");
  const tokenRef = useRef<string | null>(token);
  const connectionRef = useRef<LiveConnection | null>(null);
  const statusRef = useRef<LiveStatus>("offline");
  const subscribersRef = useRef<Set<SubscriptionEntry>>(new Set());
  const reconnectListenersRef = useRef<Set<() => void>>(new Set());
  const watchedRecordsRef = useRef<Map<string, WatchedRecordEntry>>(new Map());

  useEffect(() => {
    tokenRef.current = token;
  }, [token]);

  useEffect(() => {
    statusRef.current = status;
  }, [status]);

  const dispatchSignal = useCallback((signal: LiveSignal) => {
    for (const entry of [...subscribersRef.current]) {
      if (entry.kind === signal.kind && (entry.key === null || entry.key === signal.key)) {
        entry.fn(signal);
      }
    }
  }, []);

  const notifyReconnect = useCallback(() => {
    for (const listener of [...reconnectListenersRef.current]) {
      listener();
    }
  }, []);

  const subscribeReconnect = useCallback((fn: () => void) => {
    reconnectListenersRef.current.add(fn);
    return () => {
      reconnectListenersRef.current.delete(fn);
    };
  }, []);

  const resubscribeWatchedRecords = useCallback((conn: LiveConnection) => {
    for (const entry of watchedRecordsRef.current.values()) {
      void conn.invoke("WatchRecord", entry.type, entry.id).catch(() => {});
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      setStatus("offline");
      return;
    }

    let cancelled = false;
    let retryTimer: number | null = null;
    let hasDisconnectedOrFailed = false;

    const conn = connectionProp ?? createHubConnection(() => {
      return localStorage.getItem("microlims_token") ?? tokenRef.current ?? "";
    });
    connectionRef.current = conn;

    const handleSignal = (signal: LiveSignal) => {
      if (cancelled) return;
      dispatchSignal(signal);
    };

    const scheduleRetry = () => {
      if (cancelled || retryTimer !== null) return;
      retryTimer = window.setTimeout(() => {
        retryTimer = null;
        startConnection(true);
      }, RECONNECT_RETRY_MS);
    };

    const startConnection = (isRetry: boolean) => {
      if (cancelled) return;
      if (!isRetry) {
        statusRef.current = "connecting";
        setStatus("connecting");
      }

      conn.start()
        .then(() => {
          if (cancelled) {
            void conn.stop().catch(() => {});
            return;
          }
          statusRef.current = "connected";
          setStatus("connected");
          resubscribeWatchedRecords(conn);
          if (hasDisconnectedOrFailed) {
            hasDisconnectedOrFailed = false;
            notifyReconnect();
          }
        })
        .catch(() => {
          if (cancelled) return;
          hasDisconnectedOrFailed = true;
          statusRef.current = "offline";
          setStatus("offline");
          scheduleRetry();
        });
    };

    conn.on("signal", handleSignal);
    conn.onreconnecting(() => {
      if (cancelled) return;
      hasDisconnectedOrFailed = true;
      statusRef.current = "connecting";
      setStatus("connecting");
    });
    conn.onreconnected(() => {
      if (cancelled) return;
      hasDisconnectedOrFailed = false;
      statusRef.current = "connected";
      setStatus("connected");
      resubscribeWatchedRecords(conn);
      notifyReconnect();
    });
    conn.onclose(() => {
      if (cancelled) return;
      hasDisconnectedOrFailed = true;
      statusRef.current = "offline";
      setStatus("offline");
      scheduleRetry();
    });

    startConnection(false);

    return () => {
      cancelled = true;
      if (retryTimer !== null) {
        window.clearTimeout(retryTimer);
        retryTimer = null;
      }
      conn.off("signal", handleSignal);
      if (connectionRef.current === conn) {
        connectionRef.current = null;
      }
      statusRef.current = "offline";
      setStatus("offline");
      void conn.stop().catch(() => {});
    };
  }, [isAuthenticated, userId, connectionProp, dispatchSignal, notifyReconnect, resubscribeWatchedRecords]);

  const subscribe = useCallback((kind: string, key: string | null, fn: (s: LiveSignal) => void) => {
    const entry: SubscriptionEntry = { kind, key, fn };
    subscribersRef.current.add(entry);
    return () => {
      subscribersRef.current.delete(entry);
    };
  }, []);

  const watchRecord = useCallback((type: string, id: number) => {
    const recordKey = `${type}:${id}`;
    const existing = watchedRecordsRef.current.get(recordKey);
    if (existing) {
      existing.count += 1;
    } else {
      watchedRecordsRef.current.set(recordKey, { type, id, count: 1 });
      if (statusRef.current === "connected" && connectionRef.current) {
        void connectionRef.current.invoke("WatchRecord", type, id).catch(() => {});
      }
    }

    let released = false;
    return () => {
      if (released) return;
      released = true;
      const current = watchedRecordsRef.current.get(recordKey);
      if (!current) return;
      current.count -= 1;
      if (current.count <= 0) {
        watchedRecordsRef.current.delete(recordKey);
        if (statusRef.current === "connected" && connectionRef.current) {
          void connectionRef.current.invoke("UnwatchRecord", type, id).catch(() => {});
        }
      }
    };
  }, []);

  const value = useMemo<LiveContextValue>(
    () => ({ status, subscribe, watchRecord }),
    [status, subscribe, watchRecord]
  );

  return (
    <LiveContext.Provider value={value}>
      <LiveReconnectContext.Provider value={subscribeReconnect}>
        {children}
      </LiveReconnectContext.Provider>
    </LiveContext.Provider>
  );
}

export function useLive(): LiveContextValue {
  return useContext(LiveContext);
}

export function useLiveReconnectSubscription(): (fn: () => void) => () => void {
  return useContext(LiveReconnectContext);
}
