import React from "react";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import LandingPage from "#/page/landing/LandingPage";
import DashboardLayout from "#/components/layout/DashboardLayout";
import DashboardHome from "#/page/dashboard-home/DashboardHome";
import EmbeddingsPage from "#/page/embeddings/EmbeddingsPage";
import ComplitionPage from "#/page/completion/ComplitionPage";
import ChatCompletionPage from "#/page/chat-completion/ChatCompletionPage";
import ConfigurationPage from "#/page/configuration/ConfigurationPage";

const App: React.FC = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/dashboard" element={<DashboardLayout />}>
          <Route index element={<DashboardHome />} />
          <Route path="embeddings" element={<EmbeddingsPage />} />
          <Route path="completions" element={<ComplitionPage />} />
          <Route path="chat-completion" element={<ChatCompletionPage />} />
          <Route path="configuration" element={<ConfigurationPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
};

export default App;
