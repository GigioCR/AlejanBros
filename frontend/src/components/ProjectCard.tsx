import { useState } from 'react';
import { type Project } from '../lib/api';
import { Users, Calendar } from 'lucide-react';

interface ProjectCardProps {
  project: Project;
}

export function ProjectCard({ project }: ProjectCardProps) {
  const [showDescription, setShowDescription] = useState(false);
  const [showAllSkills, setShowAllSkills] = useState(false);

  const getStatusLabel = (status: string | number): string => {
    const statusMap: Record<number, string> = {
      0: 'Planning',
      1: 'Active',
      2: 'OnHold',
      3: 'Completed',
    };
    if (typeof status === 'number') {
      return statusMap[status] || 'Unknown';
    }
    return status || 'Unknown';
  };

  const getStatusColor = (status: string | number) => {
    const statusStr = typeof status === 'number' ? getStatusLabel(status) : status;
    switch (statusStr?.toLowerCase()) {
      case 'active': return 'bg-green-500/20 text-green-400';
      case 'planning': return 'bg-blue-500/20 text-blue-400';
      case 'completed': return 'bg-gray-500/20 text-gray-400';
      case 'onhold':
      case 'on-hold': return 'bg-yellow-500/20 text-yellow-400';
      default: return 'bg-gray-500/20 text-gray-400';
    }
  };

  const visibleSkills = project.requiredSkills?.slice(0, 4) || [];
  const hiddenSkills = project.requiredSkills?.slice(4) || [];
  const hasMoreSkills = hiddenSkills.length > 0;
  const description = project.description || 'No description';
  const isDescriptionLong = description.length > 80;

  return (
    <div className="bg-gray-800 rounded-xl border border-gray-700 p-5 hover:border-gray-600 transition-colors">
      <div className="flex items-start justify-between mb-3">
        <div>
          <h3 className="font-semibold text-white text-lg">{project.name}</h3>
          <span className={`inline-block mt-1 px-2 py-0.5 text-xs font-medium rounded-full ${getStatusColor(project.status)}`}>
            {getStatusLabel(project.status)}
          </span>
        </div>
      </div>

      <div className="relative">
        <p 
          className={`text-gray-400 text-sm mb-4 line-clamp-2 ${isDescriptionLong ? 'cursor-pointer hover:text-gray-300' : ''}`}
          onMouseEnter={() => isDescriptionLong && setShowDescription(true)}
          onMouseLeave={() => setShowDescription(false)}
        >
          {description}
        </p>
        {showDescription && isDescriptionLong && (
          <div className="absolute bottom-full left-0 mb-2 p-3 bg-gray-900 border border-gray-600 rounded-lg shadow-xl z-10 max-w-[300px]">
            <p className="text-xs text-gray-400 mb-1 font-medium">Full Description:</p>
            <p className="text-sm text-gray-300">{description}</p>
          </div>
        )}
      </div>

      <div className="space-y-2 text-sm">
        <div className="flex items-center gap-2 text-gray-400">
          <Users className="w-4 h-4" />
          <span>Team size: {project.teamSize}</span>
        </div>
        <div className="flex items-center gap-2 text-gray-400">
          <Calendar className="w-4 h-4" />
          <span>Created: {new Date(project.createdAt).toLocaleDateString()}</span>
        </div>
      </div>

      {project.requiredSkills && project.requiredSkills.length > 0 && (
        <div className="mt-4 flex flex-wrap gap-1">
          {visibleSkills.map((skill, i) => (
            <span
              key={i}
              className="px-2 py-1 bg-purple-500/20 text-purple-300 text-xs rounded-full"
            >
              {skill.name}
            </span>
          ))}
          {hasMoreSkills && (
            <div className="relative">
              <span
                className="px-2 py-1 bg-gray-700 text-gray-400 text-xs rounded-full cursor-pointer hover:bg-gray-600 hover:text-gray-300 transition-colors"
                onMouseEnter={() => setShowAllSkills(true)}
                onMouseLeave={() => setShowAllSkills(false)}
              >
                +{hiddenSkills.length} more
              </span>
              {showAllSkills && (
                <div className="absolute bottom-full left-0 mb-2 p-3 bg-gray-900 border border-gray-600 rounded-lg shadow-xl z-10 min-w-[200px]">
                  <p className="text-xs text-gray-400 mb-2 font-medium">Additional Skills:</p>
                  <div className="flex flex-wrap gap-1">
                    {hiddenSkills.map((skill, i) => (
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
          )}
        </div>
      )}
    </div>
  );
}
