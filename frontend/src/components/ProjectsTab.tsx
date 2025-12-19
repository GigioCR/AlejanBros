import { useState, useEffect } from 'react';
import { api, type Project, type PaginatedResult } from '../lib/api';
import { FolderKanban, RefreshCw } from 'lucide-react';
import { Pagination } from './Pagination';
import { ProjectCard } from './ProjectCard';

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
            <ProjectCard key={project.id} project={project} />
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
