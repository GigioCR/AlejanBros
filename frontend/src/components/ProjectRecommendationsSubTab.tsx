import { useState, useEffect } from 'react';
import { api, type MatchResult, type Project } from '../lib/api';
import { Loader2, Star, Sparkles } from 'lucide-react';
import { MatchCard } from './MatchCard';

export function ProjectRecommendationsSubTab() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('');
  const [isLoadingProjects, setIsLoadingProjects] = useState(false);
  const [projectMatches, setProjectMatches] = useState<MatchResult[]>([]);
  const [isLoadingMatches, setIsLoadingMatches] = useState(false);
  const [teamSize, setTeamSize] = useState(5);
  const [error, setError] = useState('');

  useEffect(() => {
    loadProjects();
  }, []);

  const loadProjects = async () => {
    setIsLoadingProjects(true);
    try {
      const result = await api.getProjects(1, 50);
      setProjects(result.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load projects');
    } finally {
      setIsLoadingProjects(false);
    }
  };

  const handleProjectMatch = async () => {
    if (!selectedProjectId) return;
    setIsLoadingMatches(true);
    setError('');
    try {
      const result = await api.findMatchesForProject(selectedProjectId, teamSize);
      setProjectMatches(result.matches || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to find matches');
      setProjectMatches([]);
    } finally {
      setIsLoadingMatches(false);
    }
  };

  const selectedProject = projects.find(p => p.id === selectedProjectId);

  return (
    <div className="flex-1 bg-gray-800 rounded-xl border border-gray-700 p-6 overflow-auto">
      <div className="space-y-6">
        {/* Project Selection */}
        <div>
          <label className="block text-sm font-medium text-gray-200 mb-2">
            Select a project to get team recommendations
          </label>
          {isLoadingProjects ? (
            <div className="flex items-center gap-2 text-gray-400 py-3">
              <Loader2 className="w-4 h-4 animate-spin" />
              Loading projects...
            </div>
          ) : (
            <select
              value={selectedProjectId}
              onChange={(e) => setSelectedProjectId(e.target.value)}
              className="w-full px-4 py-3 bg-gray-900 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent"
            >
              <option value="">-- Select a project --</option>
              {projects.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.name}
                </option>
              ))}
            </select>
          )}
        </div>

        {/* Selected Project Details */}
        {selectedProject && (
          <div className="p-4 bg-gray-900 rounded-lg border border-gray-700">
            <div className="flex items-start gap-3">
              <Sparkles className="w-5 h-5 text-purple-400 mt-0.5" />
              <div className="flex-1">
                <h4 className="font-medium text-white">{selectedProject.name}</h4>
                <p className="text-sm text-gray-400 mt-1">{selectedProject.description}</p>
                {selectedProject.requiredSkills && selectedProject.requiredSkills.length > 0 && (
                  <div className="mt-3">
                    <p className="text-xs text-gray-500 mb-2">Required Skills:</p>
                    <div className="flex flex-wrap gap-1">
                      {selectedProject.requiredSkills.map((skill, i) => (
                        <span
                          key={i}
                          className="px-2 py-1 bg-purple-500/20 text-purple-300 text-xs rounded-full"
                        >
                          {skill.name}
                        </span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Team Size & Search Button */}
        <div className="flex items-end gap-4">
          <div className="w-32">
            <label className="block text-sm font-medium text-gray-200 mb-2">
              Team Size
            </label>
            <input
              type="number"
              min={1}
              max={20}
              value={teamSize}
              onChange={(e) => setTeamSize(parseInt(e.target.value) || 5)}
              className="w-full px-4 py-3 bg-gray-900 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent"
            />
          </div>
          <button
            onClick={handleProjectMatch}
            disabled={isLoadingMatches || !selectedProjectId}
            className="px-6 py-3 bg-gradient-to-r from-purple-600 to-pink-600 hover:from-purple-700 hover:to-pink-700 disabled:from-gray-600 disabled:to-gray-600 disabled:cursor-not-allowed text-white font-semibold rounded-lg transition-all flex items-center gap-2"
          >
            {isLoadingMatches ? (
              <>
                <Loader2 className="w-5 h-5 animate-spin" />
                Finding matches...
              </>
            ) : (
              <>
                <Sparkles className="w-5 h-5" />
                Get Recommendations
              </>
            )}
          </button>
        </div>

        {/* Error */}
        {error && (
          <div className="p-4 bg-red-500/20 border border-red-500/50 rounded-lg text-red-200">
            {error}
          </div>
        )}

        {/* Results */}
        {projectMatches.length > 0 && (
          <div>
            <h3 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
              <Star className="w-5 h-5 text-yellow-400" />
              Recommended Team Members ({projectMatches.length})
            </h3>
            <div className="space-y-3">
              {projectMatches.map((match, index) => (
                <MatchCard key={match.employee.id} match={match} rank={index + 1} />
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
