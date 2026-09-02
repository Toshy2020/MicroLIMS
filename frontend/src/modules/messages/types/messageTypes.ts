export interface ConversationParticipant {
  userId: number;
  fullName: string;
  username: string;
  jobTitle?: string | null;
  roleName: string;
  lastReadAt?: string | null;
}

export interface DirectMessage {
  id: number;
  conversationId: number;
  senderUserId: number;
  senderName: string;
  senderRole?: string | null;
  content: string;
  createdAt: string;
}

export interface ConversationSummary {
  id: number;
  title?: string | null;
  isGroup: boolean;
  createdByUserId: number;
  lastMessageAt: string;
  participants: ConversationParticipant[];
  lastMessage?: DirectMessage | null;
  unreadCount: number;
}

export interface UserDirectoryItem {
  id: number;
  fullName: string;
  username: string;
  jobTitle?: string | null;
  roleName: string;
  isActive: boolean;
}

export interface CreateConversationRequest {
  title?: string | null;
  isGroup: boolean;
  participantUserIds: number[];
  initialMessage: string;
}

export interface SendMessageRequest {
  content: string;
}
