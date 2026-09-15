import { useEffect, useState } from "react";
import { masterDataOptions, MediaProductOption } from "../services/masterDataOptions";

export type { MediaProductOption };

// Backs media product pickers and configuration screens with the
// canonical MediaProduct master list (GET /masterdata/media-products).
export function useMediaProducts() {
  const [options, setOptions] = useState<MediaProductOption[]>([]);
  const [loading, setLoading] = useState(true);

  const reload = () =>
    masterDataOptions.getMediaProducts().then((data: MediaProductOption[]) => {
      setOptions(data);
      setLoading(false);
      return data;
    });

  useEffect(() => {
    reload();
  }, []);

  const create = async (name: string, code: string) => {
    const created = await masterDataOptions.createMediaProduct(name, code);
    await reload();
    return created as MediaProductOption;
  };

  const rename = async (id: number, name: string) => {
    const updated = await masterDataOptions.renameMediaProduct(id, name);
    await reload();
    return updated as MediaProductOption;
  };

  const changeCode = async (id: number, code: string, reason: string, password: string) => {
    const updated = await masterDataOptions.changeMediaProductCode(id, code, reason, password);
    await reload();
    return updated as MediaProductOption;
  };

  const remove = async (id: number) => {
    await masterDataOptions.deleteMediaProduct(id);
    await reload();
  };

  return { options, loading, reload, create, rename, changeCode, remove };
}
