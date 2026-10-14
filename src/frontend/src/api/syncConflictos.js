import { apiClient } from '@/api/apiClient';

export const syncConflictosApi = {
  pendientes: (take = 50) =>
    apiClient.get(
      `/sync/v2/conflictos?take=${encodeURIComponent(take)}`
    ),

  estado: () =>
    apiClient.get('/sync/v2/estado'),

  resolver: (conflictoId, accion, pacienteCentralId, motivo) =>
    apiClient.post(
      `/sync/v2/conflictos/${encodeURIComponent(conflictoId)}/resolver`,
      {
        accion,
        pacienteCentralId,
        motivo
      }
    )
};