import { useState } from 'react';
import { MessageSquare, FolderKanban } from 'lucide-react';
import { ChatSubTab } from './ChatSubTab';
import { ProjectRecommendationsSubTab } from './ProjectRecommendationsSubTab';

type SubTab = 'chat' | 'project';

export function AiMatchesTab() {
  const [activeSubTab, setActiveSubTab] = useState<SubTab>('chat');

  return (
    <div className="flex flex-col h-[calc(100vh-220px)]">
      <div className="mb-4">
        <h2 className="text-2xl font-bold text-white">AI-Powered Team Matching</h2>
        <p className="text-gray-400">Find the perfect team members through chat or by selecting an existing project</p>
      </div>

      {/* Sub-tab Navigation */}
      <div className="flex gap-2 mb-4">
        <button
          onClick={() => setActiveSubTab('chat')}
          className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-2 ${
            activeSubTab === 'chat'
              ? 'bg-purple-600 text-white'
              : 'bg-gray-800 text-gray-400 hover:text-white hover:bg-gray-700'
          }`}
        >
          <MessageSquare className="w-4 h-4" />
          Chat with AI
        </button>
        <button
          onClick={() => setActiveSubTab('project')}
          className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-2 ${
            activeSubTab === 'project'
              ? 'bg-purple-600 text-white'
              : 'bg-gray-800 text-gray-400 hover:text-white hover:bg-gray-700'
          }`}
        >
          <FolderKanban className="w-4 h-4" />
          Project Recommendations
        </button>
      </div>

      {/* Sub-tab Content */}
      {activeSubTab === 'chat' && <ChatSubTab />}
      {activeSubTab === 'project' && <ProjectRecommendationsSubTab />}
    </div>
  );
}
