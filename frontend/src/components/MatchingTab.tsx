import { useState } from 'react';
import { api, type MatchResult } from '../lib/api';
import { Search, Users, Star, Zap, Loader2 } from 'lucide-react';

export function MatchingTab() {
  const [query, setQuery] = useState('');
  const [teamSize, setTeamSize] = useState(5);
  const [matches, setMatches] = useState<MatchResult[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [hasSearched, setHasSearched] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;

    setIsLoading(true);
    setError('');
    setHasSearched(true);

    try {
      const result = await api.findMatches(query, teamSize);
      setMatches(result.matches || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to find matches');
      setMatches([]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-2xl font-bold text-white">AI-Powered Matching</h2>
        <p className="text-gray-400">Find the perfect team members for your project requirements</p>
      </div>

      <form onSubmit={handleSearch} className="mb-8">
        <div className="bg-gray-800 rounded-xl border border-gray-700 p-6">
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-200 mb-2">
                Describe your project requirements
              </label>
              <textarea
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="e.g., I need a team for a React web application with Node.js backend, experience in Azure cloud services, and machine learning capabilities..."
                className="w-full h-32 px-4 py-3 bg-gray-900 border border-gray-600 rounded-lg text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-none"
              />
            </div>

            <div className="flex items-end gap-4">
              <div className="flex-1 max-w-xs">
                <label className="block text-sm font-medium text-gray-200 mb-2">
                  Team Size
                </label>
                <input
                  type="number"
                  min={1}
                  max={20}
                  value={teamSize}
                  onChange={(e) => setTeamSize(parseInt(e.target.value) || 5)}
                  className="w-full px-4 py-3 bg-gray-900 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
              </div>

              <button
                type="submit"
                disabled={isLoading || !query.trim()}
                className="px-6 py-3 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 disabled:from-gray-600 disabled:to-gray-600 disabled:cursor-not-allowed text-white font-semibold rounded-lg transition-all flex items-center gap-2"
              >
                {isLoading ? (
                  <>
                    <Loader2 className="w-5 h-5 animate-spin" />
                    Searching...
                  </>
                ) : (
                  <>
                    <Search className="w-5 h-5" />
                    Find Matches
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      </form>

      {error && (
        <div className="mb-4 p-4 bg-red-500/20 border border-red-500/50 rounded-lg text-red-200">
          {error}
        </div>
      )}

      {hasSearched && !isLoading && matches.length === 0 && !error && (
        <div className="text-center py-12 bg-gray-800/50 rounded-xl border border-gray-700">
          <Users className="w-12 h-12 text-gray-600 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-300">No matches found</h3>
          <p className="text-gray-500">Try adjusting your search criteria</p>
        </div>
      )}

      {matches.length > 0 && (
        <div>
          <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <Zap className="w-5 h-5 text-yellow-400" />
            Top Matches ({matches.length})
          </h3>
          <div className="space-y-4">
            {matches.map((match, index) => (
              <div
                key={match.employee.id}
                className="bg-gray-800 rounded-xl border border-gray-700 p-5 hover:border-gray-600 transition-colors"
              >
                <div className="flex items-start gap-4">
                  <div className="flex-shrink-0 w-12 h-12 bg-gradient-to-br from-yellow-500 to-orange-600 rounded-full flex items-center justify-center text-white font-bold text-lg">
                    #{index + 1}
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-start justify-between mb-2">
                      <div>
                        <h4 className="font-semibold text-white text-lg">{match.employee.name}</h4>
                        <p className="text-gray-400">{match.employee.title} • {match.employee.department}</p>
                      </div>
                      <div className="flex items-center gap-1 px-3 py-1 bg-green-500/20 text-green-400 rounded-full">
                        <Star className="w-4 h-4" />
                        <span className="font-semibold">{Math.round(match.matchScore * 100)}%</span>
                      </div>
                    </div>

                    {match.matchReasons && match.matchReasons.length > 0 && (
                      <ul className="text-gray-300 text-sm mb-3 list-disc list-inside">
                        {match.matchReasons.map((reason, i) => (
                          <li key={i}>{reason}</li>
                        ))}
                      </ul>
                    )}

                    {match.gaps && match.gaps.length > 0 && (
                      <div className="flex flex-wrap gap-1">
                        {match.gaps.map((gap, i) => (
                          <span
                            key={i}
                            className="px-2 py-1 bg-yellow-500/20 text-yellow-300 text-xs rounded-full"
                          >
                            {gap}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
