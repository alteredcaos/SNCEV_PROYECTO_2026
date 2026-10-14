import React, { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { apiClient } from "@/api/apiClient";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Lock, Loader2, AlertTriangle, ArrowLeft } from "lucide-react";
import AuthLayout from "@/components/AuthLayout";

export default function ResetPassword() {
  const [searchParams] = useSearchParams();
  const resetToken = searchParams.get("token");

  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    if (newPassword !== confirmPassword) {
      setError("Las contraseñas no coinciden");
      return;
    }
    setLoading(true);
    try {
      await apiClient.auth.resetPassword({ resetToken, newPassword });
      window.location.href = "/login";
    } catch (err) {
      setError(err.message || "Error al restablecer la contraseña");
    } finally {
      setLoading(false);
    }
  };

  if (!resetToken) {
    return (
      <AuthLayout
        icon={AlertTriangle}
        title="Enlace no válido"
        subtitle="El token de restablecimiento es requerido"
        footer={
          <Link to="/forgot-password" className="text-primary font-medium hover:underline inline-flex items-center text-sm">
            <ArrowLeft className="w-3.5 h-3.5 mr-1" /> Solicitar nuevo enlace
          </Link>
        }
      >
        <p className="text-sm text-foreground text-center">
          El enlace utilizado no es válido o ha expirado. Por favor solicita un nuevo restablecimiento de contraseña.
        </p>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      icon={Lock}
      title="Nueva Contraseña"
      subtitle="Ingresa tu nueva contraseña para acceder al sistema"
      footer={
        <Link to="/login" className="text-primary font-medium hover:underline inline-flex items-center text-sm">
          <ArrowLeft className="w-3.5 h-3.5 mr-1" /> Volver al inicio de sesión
        </Link>
      }
    >
      {error && (
        <div className="mb-4 p-3 rounded-lg bg-destructive/10 text-destructive text-sm">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="newPassword">Nueva Contraseña</Label>
          <Input
            id="newPassword"
            type="password"
            autoComplete="new-password"
            placeholder="Mínimo 12 caracteres"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            className="h-12"
            required
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="confirmPassword">Confirmar Contraseña</Label>
          <Input
            id="confirmPassword"
            type="password"
            autoComplete="new-password"
            placeholder="Repite la contraseña"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            className="h-12"
            required
          />
        </div>
        <Button type="submit" className="w-full h-12 font-medium" disabled={loading}>
          {loading ? (
            <>
              <Loader2 className="w-4 h-4 mr-2 animate-spin" />
              Guardando contraseña...
            </>
          ) : (
            "Actualizar Contraseña"
          )}
        </Button>
      </form>
    </AuthLayout>
  );
}
