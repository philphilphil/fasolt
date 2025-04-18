import type { LoginResponse } from '@/api/models';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import router from '@/router';
import { useAuthStore } from '@/stores/authStore';

export function useAuthService(api: SpacedMdApiClient) {
  const authStore = useAuthStore();

  async function login(email: string, password: string): Promise<LoginResponse> {
    try {
      var loginResult = await api.login.post(
        {
          email,
          password,
        }
      );
      authStore.setUser({ email })
      return loginResult!;
    } catch (error) {
      throw new Error('Login failed: ' + error);
    }
  }

  async function logout(): Promise<void> {
    try {
      await api.logout.post();
      authStore.isAuthenticated = false;
      authStore.user = null;
      router.push("/");
    } catch (error) {
      console.error("Failed to log out:", error);
    }
  }

  async function getUserInfo(): Promise<string | null> {
    try {
      const userInfo = await api.user.info.get();
      authStore.setUser(userInfo ? { ...userInfo, email: userInfo.email ?? '' } : null);
    } catch (error) {
      console.error('Failed to fetch user info:', error);
    }
    return null;
  }

  return { login, logout, getUserInfo };
}
