import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { LogOut, Users, FolderKanban, Search, MessageSquare } from 'lucide-react';
import { EmployeesTab } from '../components/EmployeesTab';
import { ProjectsTab } from '../components/ProjectsTab';
import { MatchingTab } from '../components/MatchingTab';
import { ChatTab } from '../components/ChatTab';

type TabType = 'employees' | 'projects' | 'matching' | 'chat';

const tabs = [
  { id: 'employees' as TabType, label: 'Employees', icon: Users },
  { id: 'projects' as TabType, label: 'Projects', icon: FolderKanban },
  { id: 'matching' as TabType, label: 'AI Matching', icon: Search },
  { id: 'chat' as TabType, label: 'Chat', icon: MessageSquare },
];

export function DashboardPage() {
  const [activeTab, setActiveTab] = useState<TabType>('employees');
  const { user, logout } = useAuth();

  return (
    <div className="min-h-screen bg-gray-900">
      {/* Header */}
      <header className="bg-gray-800 border-b border-gray-700">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-gradient-to-br from-blue-500 to-purple-600 rounded-lg flex items-center justify-center">
                <Users className="w-6 h-6 text-white" />
              </div>
              <div>
                <h1 className="text-xl font-bold text-white">Project - Employee Matcher</h1>
                <p className="text-xs text-gray-400">AI-Powered Team Building</p>
              </div>
            </div>

            <div className="flex items-center gap-4">
              <div className="text-right">
                <p className="text-sm font-medium text-white">{user?.name}</p>
                <p className="text-xs text-gray-400">{user?.email}</p>
              </div>
              <button
                onClick={logout}
                className="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                title="Sign out"
              >
                <LogOut className="w-5 h-5" />
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Tabs */}
      <div className="bg-gray-800/50 border-b border-gray-700">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <nav className="flex gap-1">
            {tabs.map((tab) => {
              const Icon = tab.icon;
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
                    isActive
                      ? 'border-blue-500 text-blue-400'
                      : 'border-transparent text-gray-400 hover:text-gray-200 hover:border-gray-600'
                  }`}
                >
                  <Icon className="w-4 h-4" />
                  {tab.label}
                </button>
              );
            })}
          </nav>
        </div>
      </div>

      {/* Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {activeTab === 'employees' && <EmployeesTab />}
        {activeTab === 'projects' && <ProjectsTab />}
        {activeTab === 'matching' && <MatchingTab />}
        {activeTab === 'chat' && <ChatTab />}
      </main>
    </div>
  );
}
