import React from "react";
import { Link } from "react-router-dom";
import { ShieldCheck, ArrowLeft } from "lucide-react";
import AuthLayout from "@/components/AuthLayout";

export default function OAuthConsent() {
  return (
    <AuthLayout
      icon={ShieldCheck}
      title="Autorización SNCEV"
      subtitle="Sistema Nacional de Cartilla Electrónica de Vacunación"
      footer={
        <Link to="/" className="text-primary font-medium hover:underline inline-flex items-center text-sm">
          <ArrowLeft className="w-3.5 h-3.5 mr-1" /> Ir al Panel Principal
        </Link>
      }
    >
      <div className="space-y-4 text-center">
        <p className="text-sm text-foreground">
          Este módulo está configurado para acceso institucional local.
        </p>
      </div>
    </AuthLayout>
  );
}
