import React, { useState } from "react";
import { Link } from "react-router-dom";
import { apiClient } from "@/api/apiClient";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Mail, ArrowLeft, Loader2 } from "lucide-react";
import AuthLayout from "@/components/AuthLayout";

export default function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await apiClient.auth.resetPasswordRequest(email);
    } catch {
      // Siempre confirmamos por seguridad
    } finally {
      setLoading(false);
      setSent(true);
    }
  };

  return (
    <AuthLayout
      icon={Mail}
      title="Restablecer Contraseña"
      subtitle="Ingresa tu correo para recibir las instrucciones"
      footer={
        <Link to="/login" className="text-primary font-medium hover:underline inline-flex items-center text-sm">
          <ArrowLeft className="w-3.5 h-3.5 mr-1" /> Volver al inicio de sesión
        </Link>
      }
    >
      {sent ? (
        <div className="space-y-4 text-center">
          <p className="text-sm text-foreground">
            Si la cuenta existe en el sistema, recibirás las instrucciones para restablecer tu contraseña.
          </p>
          <Button asChild variant="outline" className="w-full">
            <Link to="/login">Regresar a Iniciar Sesión</Link>
          </Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">Correo Institucional</Label>
            <div className="relative">
              <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" aria-hidden="true" />
              <Input
                id="email"
                type="email"
                autoComplete="email"
                autoFocus
                placeholder="tu.nombre@salud.gob.mx"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="pl-10 h-12"
                required
              />
            </div>
          </div>
          <Button type="submit" className="w-full h-12 font-medium" disabled={loading}>
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                Enviando instrucciones...
              </>
            ) : (
              "Enviar Instrucciones"
            )}
          </Button>
        </form>
      )}
    </AuthLayout>
  );
}
