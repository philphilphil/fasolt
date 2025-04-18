import { useToast, type ToastServiceMethods } from 'primevue';

const tempToastLifetimeMs = 3000;

export function useToastService(toast: ToastServiceMethods) {

  async function tempSuccessToast(message: string): Promise<void> {
    toast.add({
      severity: 'success',
      summary: 'Success',
      detail: message,
      life: tempToastLifetimeMs,
    });
  }

  async function failToast(message: string): Promise<void> {
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: message,
    });
  }

  async function tempInfoToast(message: string): Promise<void> {
    toast.add({
      severity: 'info',
      summary: 'Info',
      detail: message,
      life: tempToastLifetimeMs,
    });
  }

  return { tempSuccessToast, failToast, tempInfoToast };
}
