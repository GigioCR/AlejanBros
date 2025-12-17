import { useState, useRef, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import { api, type MatchResult } from '../lib/api';
import { Send, Bot, User, Loader2, Star, ChevronDown, ChevronUp } from 'lucide-react';

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  matches?: MatchResult[];
  timestamp: Date;
}

function MatchCard({ match, rank }: { match: MatchResult; rank: number }) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="bg-gray-800 rounded-lg border border-gray-600 p-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-gradient-to-br from-yellow-500 to-orange-600 rounded-full flex items-center justify-center text-white font-bold text-sm">
            #{rank}
          </div>
          <div>
            <p className="font-medium text-white text-sm">{match.employee.name}</p>
            <p className="text-xs text-gray-400">{match.employee.jobTitle || match.employee.title}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1 px-2 py-1 bg-green-500/20 text-green-400 rounded-full text-xs">
            <Star className="w-3 h-3" />
            <span className="font-semibold">{Math.round(match.matchScore * 100)}%</span>
          </div>
          <button
            onClick={() => setExpanded(!expanded)}
            className="p-1 text-gray-400 hover:text-white transition-colors"
          >
            {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>
        </div>
      </div>

      {expanded && (
        <div className="mt-3 pt-3 border-t border-gray-600 space-y-2">
          {match.matchReasons && match.matchReasons.length > 0 && (
            <div>
              <p className="text-xs font-medium text-gray-400 mb-1">Why they match:</p>
              <ul className="text-xs text-gray-300 list-disc list-inside">
                {match.matchReasons.map((reason, i) => (
                  <li key={i}>{reason}</li>
                ))}
              </ul>
            </div>
          )}
          {match.gaps && match.gaps.length > 0 && (
            <div>
              <p className="text-xs font-medium text-gray-400 mb-1">Skill gaps:</p>
              <div className="flex flex-wrap gap-1">
                {match.gaps.map((gap, i) => (
                  <span key={i} className="px-2 py-0.5 bg-yellow-500/20 text-yellow-300 text-xs rounded-full">
                    {gap}
                  </span>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export function ChatTab() {
  const [messages, setMessages] = useState<Message[]>([
    {
      id: '1',
      role: 'assistant',
      content: 'Hello! I\'m your AI assistant for finding the perfect team members. Describe your project needs and I\'ll help you find the best matches from your employee database.',
      timestamp: new Date(),
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!input.trim() || isLoading) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      role: 'user',
      content: input.trim(),
      timestamp: new Date(),
    };

    const updatedMessages = [...messages, userMessage];
    setMessages(updatedMessages);
    setInput('');
    setIsLoading(true);

    try {
      // Build conversation history for context (last 10 messages, excluding the welcome message)
      const history = updatedMessages
        .filter(m => m.id !== '1') // Exclude initial welcome message
        .slice(-10) // Keep last 10 messages for context
        .map(m => ({ role: m.role, content: m.content }));

      const response = await api.chat(userMessage.content, history);
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: response.response,
        matches: response.matches,
        timestamp: new Date(response.timestamp),
      };
      setMessages((prev) => [...prev, assistantMessage]);
    } catch (err) {
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `Sorry, I encountered an error: ${err instanceof Error ? err.message : 'Unknown error'}. Please try again.`,
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col h-[calc(100vh-220px)]">
      <div className="mb-4">
        <h2 className="text-2xl font-bold text-white">AI-Powered Team Matching</h2>
        <p className="text-gray-400">Describe your project needs and get ranked employee matches with detailed analysis</p>
      </div>

      <div className="flex-1 bg-gray-800 rounded-xl border border-gray-700 flex flex-col overflow-hidden">
        {/* Messages */}
        <div className="flex-1 overflow-y-auto p-4 space-y-4">
          {messages.map((message) => (
            <div
              key={message.id}
              className={`flex gap-3 ${message.role === 'user' ? 'flex-row-reverse' : ''}`}
            >
              <div
                className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center ${
                  message.role === 'user'
                    ? 'bg-blue-600'
                    : 'bg-gradient-to-br from-purple-500 to-pink-500'
                }`}
              >
                {message.role === 'user' ? (
                  <User className="w-4 h-4 text-white" />
                ) : (
                  <Bot className="w-4 h-4 text-white" />
                )}
              </div>
              <div
                className={`max-w-[80%] rounded-2xl px-4 py-3 ${
                  message.role === 'user'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-700 text-gray-100'
                }`}
              >
                {message.role === 'assistant' ? (
                  <>
                    {/* Match Cards */}
                    {message.matches && message.matches.length > 0 && (
                      <div className="mb-4 space-y-2">
                        <p className="text-sm font-medium text-gray-300 mb-2">Top Matches:</p>
                        {message.matches.slice(0, 5).map((match, index) => (
                          <MatchCard key={match.employee.id} match={match} rank={index + 1} />
                        ))}
                      </div>
                    )}
                    {/* Analysis Text */}
                    <div className="prose prose-invert prose-sm max-w-none prose-headings:text-gray-100 prose-p:text-gray-200 prose-strong:text-white prose-li:text-gray-200">
                      <ReactMarkdown>{message.content}</ReactMarkdown>
                    </div>
                  </>
                ) : (
                  <p className="whitespace-pre-wrap">{message.content}</p>
                )}
                <p className={`text-xs mt-1 ${message.role === 'user' ? 'text-blue-200' : 'text-gray-400'}`}>
                  {message.timestamp.toLocaleTimeString()}
                </p>
              </div>
            </div>
          ))}

          {isLoading && (
            <div className="flex gap-3">
              <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-purple-500 to-pink-500 flex items-center justify-center">
                <Bot className="w-4 h-4 text-white" />
              </div>
              <div className="bg-gray-700 rounded-2xl px-4 py-3">
                <div className="flex items-center gap-2 text-gray-300">
                  <Loader2 className="w-4 h-4 animate-spin" />
                  <span>Thinking...</span>
                </div>
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input */}
        <form onSubmit={handleSubmit} className="p-4 border-t border-gray-700">
          <div className="flex gap-3">
            <textarea
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault();
                  handleSubmit(e);
                }
              }}
              placeholder="Ask about team requirements, skills, or project needs..."
              className="flex-1 px-4 py-3 bg-gray-900 border border-gray-600 rounded-xl text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent resize-none min-h-[48px] max-h-[120px]"
              disabled={isLoading}
              rows={1}
            />
            <button
              type="submit"
              disabled={isLoading || !input.trim()}
              className="px-4 py-3 bg-purple-600 hover:bg-purple-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white rounded-xl transition-colors"
            >
              <Send className="w-5 h-5" />
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
