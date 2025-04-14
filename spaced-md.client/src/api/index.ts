// import { inject, type Plugin } from 'vue'
// import { ApiClient } from './apiClient';
// import { type AxiosInstance } from 'axios';

// const apiKey: string = "apiClient";

// type Options = {
//     axiosInstance?: AxiosInstance;
// }

// const plugin: Plugin<Options> = {
//     install(app, options) {
//         const apiClient = new ApiClient(import.meta.env.VITE_API_URL, options.axiosInstance);
//         app.provide(apiKey, apiClient);

//         app.config.globalProperties.$api = apiClient
//     }
// }

// export default plugin;

// export function useApi(): ApiClient {
//     return inject(apiKey)!
// }
