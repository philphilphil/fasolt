import type { LoginRequestBuilderPostQueryParameters } from '@/api/login';
import type { SpacedMdApiClient } from '@/api/spacedMdApiClient';
import { useAuthStore } from '@/stores/authStore';
import { inject } from 'vue';


export default class AuthService {

  private api: SpacedMdApiClient;

  constructor(api: SpacedMdApiClient) {
    this.api = api;
  }

  private get authStore() {
    return useAuthStore();
  }

  async login(email: string, password: string): Promise<boolean> {
    try {
      const loginQueryParameters: LoginRequestBuilderPostQueryParameters = {
        useCookies: true,
        useSessionCookies: false,
      };

      await this.api.login.post(
        {
          email: email,
          password: password,
        },
        { queryParameters: loginQueryParameters }
      );
    } catch (error) {
      // errorMessage.value = "Error during login." + error;
    }
    return true;
  }

  async logout(): Promise<void> {
    try {
      await this.api.logout.post();
      this.authStore.isAuthenticated = false;
      this.authStore.user = null;
    } catch (error) {
      console.error('Failed to log out:', error);
    }
  }

  async getUserInfo(): Promise<string | null> {
    try {
      const userInfo = await this.api.manage.info.get();
      this.authStore.setUser(userInfo ? { ...userInfo, email: userInfo.email ?? '' } : null);
    } catch (error) {
      console.error('Failed to fetch user info:', error);
    }
    return "";
  }
}
