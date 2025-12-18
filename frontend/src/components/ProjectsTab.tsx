import { useState, useEffect } from 'react';
import { api, type Project, type PaginatedResult } from '../lib/api';
import { Plus, Trash2, Edit, FolderKanban, RefreshCw, Users, Calendar } from 'lucide-react';
import { Pagination } from './Pagination';

export function ProjectsTab() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [paginationInfo, setPaginationInfo] = useState<Omit<PaginatedResult<Project>, 'items'> | null>(null);

  const loadProjects = async (currentPage = page, currentPageSize = pageSize) => {
    setIsLoading(true);
    setError('');
    try {
      const data = await api.getProjects(currentPage, currentPageSize);
      setProjects(data.items);
      setPaginationInfo({
        page: data.page,
        pageSize: data.pageSize,
        totalCount: data.totalCount,
        totalPages: data.totalPages,
        hasNextPage: data.hasNextPage,
        hasPreviousPage: data.hasPreviousPage,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load projects');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadProjects();
  }, []);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    loadProjects(newPage, pageSize);
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPageSize(newPageSize);
    setPage(1);
    loadProjects(1, newPageSize);
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this project?')) return;
    try {
      await api.deleteProject(id);
      setProjects(projects.filter(p => p.id !== id));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete project');
    }
  };

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

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-8 h-8 border-4 border-purple-500/30 border-t-purple-500 rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-white">Projects</h2>
          <p className="text-gray-400">Manage your projects and team requirements</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => loadProjects()}
            className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg flex items-center gap-2 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg flex items-center gap-2 transition-colors">
            <Plus className="w-4 h-4" />
            Add Project
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-500/20 border border-red-500/50 rounded-lg text-red-200">
          {error}
        </div>
      )}

      {projects.length === 0 ? (
        <div className="text-center py-12 bg-gray-800/50 rounded-xl border border-gray-700">
          <FolderKanban className="w-12 h-12 text-gray-600 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-300">No projects yet</h3>
          <p className="text-gray-500">Create your first project to get started</p>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {projects.map((project) => (
            <div
              key={project.id}
              className="bg-gray-800 rounded-xl border border-gray-700 p-5 hover:border-gray-600 transition-colors"
            >
              <div className="flex items-start justify-between mb-3">
                <div>
                  <h3 className="font-semibold text-white text-lg">{project.name}</h3>
                  <span className={`inline-block mt-1 px-2 py-0.5 text-xs font-medium rounded-full ${getStatusColor(project.status)}`}>
                    {getStatusLabel(project.status)}
                  </span>
                </div>
                <div className="flex gap-1">
                  <button className="p-2 text-gray-400 hover:text-purple-400 hover:bg-gray-700 rounded-lg transition-colors">
                    <Edit className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => handleDelete(project.id)}
                    className="p-2 text-gray-400 hover:text-red-400 hover:bg-gray-700 rounded-lg transition-colors"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>

              <p className="text-gray-400 text-sm mb-4 line-clamp-2">
                {project.description || 'No description'}
              </p>

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
                  {project.requiredSkills.slice(0, 4).map((skill, i) => (
                    <span
                      key={i}
                      className="px-2 py-1 bg-purple-500/20 text-purple-300 text-xs rounded-full"
                    >
                      {skill.name}
                    </span>
                  ))}
                  {project.requiredSkills.length > 4 && (
                    <span className="px-2 py-1 bg-gray-700 text-gray-400 text-xs rounded-full">
                      +{project.requiredSkills.length - 4} more
                    </span>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {paginationInfo && (
        <Pagination
          page={paginationInfo.page}
          pageSize={paginationInfo.pageSize}
          totalCount={paginationInfo.totalCount}
          totalPages={paginationInfo.totalPages}
          hasNextPage={paginationInfo.hasNextPage}
          hasPreviousPage={paginationInfo.hasPreviousPage}
          onPageChange={handlePageChange}
          onPageSizeChange={handlePageSizeChange}
        />
      )}
    </div>
  );
}
