import { useState } from 'react';
import { type Employee } from '../lib/api';
import { Trash2, Edit, Briefcase, Star } from 'lucide-react';

interface EmployeeCardProps {
  employee: Employee;
  onDelete: (id: string) => void;
}

export function EmployeeCard({ employee, onDelete }: EmployeeCardProps) {
  const [showAllSkills, setShowAllSkills] = useState(false);

  const getAvailabilityLabel = (availability: number | string): string => {
    const statusMap: Record<number, string> = {
      0: 'Available',
      1: 'Partially Available',
      2: 'Unavailable',
    };
    if (typeof availability === 'number') {
      return statusMap[availability] || 'Unknown';
    }
    return String(availability) || 'Unknown';
  };

  const getAvailabilityStyle = (availability: number | string): string => {
    const status = typeof availability === 'number' ? availability : parseInt(String(availability), 10);
    switch (status) {
      case 0: return 'bg-green-500/20 text-green-400';
      case 1: return 'bg-yellow-500/20 text-yellow-400';
      case 2: return 'bg-red-500/20 text-red-400';
      default: return 'bg-gray-500/20 text-gray-400';
    }
  };

  const visibleSkills = employee.skills?.slice(0, 4) || [];
  const hiddenSkills = employee.skills?.slice(4) || [];
  const hasMoreSkills = hiddenSkills.length > 0;

  return (
    <div className="bg-gray-800 rounded-xl border border-gray-700 p-5 hover:border-gray-600 transition-colors">
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center text-white font-bold text-lg">
            {employee.name?.charAt(0) || 'E'}
          </div>
          <div>
            <h3 className="font-semibold text-white">{employee.name}</h3>
            <p className="text-sm text-gray-400">{employee.title}</p>
          </div>
        </div>
        <div className="flex gap-1">
          <button className="p-2 text-gray-400 hover:text-blue-400 hover:bg-gray-700 rounded-lg transition-colors">
            <Edit className="w-4 h-4" />
          </button>
          <button
            onClick={() => onDelete(employee.id)}
            className="p-2 text-gray-400 hover:text-red-400 hover:bg-gray-700 rounded-lg transition-colors"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      </div>

      <div className="space-y-2 text-sm">
        <div className="flex items-center gap-2 text-gray-400">
          <Briefcase className="w-4 h-4" />
          <span>{employee.department}</span>
        </div>
        <div className="flex items-center gap-2 text-gray-400">
          <Star className="w-4 h-4" />
          <span>{employee.yearsOfExperience} years experience</span>
        </div>
      </div>

      {employee.skills && employee.skills.length > 0 && (
        <div className="mt-4 flex flex-wrap gap-1">
          {visibleSkills.map((skill, i) => (
            <span
              key={i}
              className="px-2 py-1 bg-blue-500/20 text-blue-300 text-xs rounded-full"
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
                        className="px-2 py-1 bg-blue-500/20 text-blue-300 text-xs rounded-full"
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

      <div className="mt-4 pt-3 border-t border-gray-700">
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-400">Availability</span>
          <span className={`px-2 py-0.5 text-xs font-medium rounded-full ${getAvailabilityStyle(employee.availability)}`}>
            {getAvailabilityLabel(employee.availability)}
          </span>
        </div>
      </div>
    </div>
  );
}
