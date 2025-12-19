import { useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { type MatchResult } from '../lib/api';

interface MatchCardProps {
  match: MatchResult;
  rank: number;
}

function getFitLabel(score: number): { label: string; colorClass: string } {
  const percent = score * 100;
  if (percent >= 90) return { label: 'Excellent Fit', colorClass: 'bg-emerald-500/20 text-emerald-400' };
  if (percent >= 75) return { label: 'Great Fit', colorClass: 'bg-green-500/20 text-green-400' };
  if (percent >= 60) return { label: 'Good Fit', colorClass: 'bg-yellow-500/20 text-yellow-400' };
  if (percent >= 40) return { label: 'Moderate Fit', colorClass: 'bg-orange-500/20 text-orange-400' };
  return { label: 'Weak Fit', colorClass: 'bg-red-500/20 text-red-400' };
}

export function MatchCard({ match, rank }: MatchCardProps) {
  const [expanded, setExpanded] = useState(false);
  const fit = getFitLabel(match.matchScore);

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
          <div className={`flex items-center gap-1 px-2 py-1 rounded-full text-xs ${fit.colorClass}`}>
            <span className="font-semibold">{fit.label}</span>
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
          {match.bonusReasons && match.bonusReasons.length > 0 && (
            <div>
              <p className="text-xs font-medium text-gray-400 mb-1">Bonus strengths:</p>
              <ul className="text-xs text-gray-300 list-disc list-inside">
                {match.bonusReasons.map((reason, i) => (
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
