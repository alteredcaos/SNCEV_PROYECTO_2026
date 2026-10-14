// =============================================================================
// Re-export compatible para componentes existentes
// Enrutado al cliente nativo local/producción apiClient
// =============================================================================
import { apiClient } from './apiClient';

export const base44 = apiClient;
export default apiClient;
