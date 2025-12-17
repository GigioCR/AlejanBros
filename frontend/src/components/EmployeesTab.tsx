import { useState, useEffect } from 'react';
import { api, type Employee } from '../lib/api';
import { Plus, Trash2, Edit, User, Briefcase, Star, RefreshCw } from 'lucide-react';

export function EmployeesTab() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  const loadEmployees = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await api.getEmployees();
      setEmployees(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load employees');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadEmployees();
  }, []);

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this employee?')) return;
    try {
      await api.deleteEmployee(id);
      setEmployees(employees.filter(e => e.id !== id));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete employee');
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-8 h-8 border-4 border-blue-500/30 border-t-blue-500 rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-white">Employees</h2>
          <p className="text-gray-400">Manage your team members</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={loadEmployees}
            className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg flex items-center gap-2 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg flex items-center gap-2 transition-colors">
            <Plus className="w-4 h-4" />
            Add Employee
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-500/20 border border-red-500/50 rounded-lg text-red-200">
          {error}
        </div>
      )}

      {employees.length === 0 ? (
        <div className="text-center py-12 bg-gray-800/50 rounded-xl border border-gray-700">
          <User className="w-12 h-12 text-gray-600 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-300">No employees yet</h3>
          <p className="text-gray-500">Add your first team member to get started</p>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {employees.map((employee) => (
            <div
              key={employee.id}
              className="bg-gray-800 rounded-xl border border-gray-700 p-5 hover:border-gray-600 transition-colors"
            >
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
                    onClick={() => handleDelete(employee.id)}
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
                  {employee.skills.slice(0, 4).map((skill, i) => (
                    <span
                      key={i}
                      className="px-2 py-1 bg-blue-500/20 text-blue-300 text-xs rounded-full"
                    >
                      {skill.name}
                    </span>
                  ))}
                  {employee.skills.length > 4 && (
                    <span className="px-2 py-1 bg-gray-700 text-gray-400 text-xs rounded-full">
                      +{employee.skills.length - 4} more
                    </span>
                  )}
                </div>
              )}

              <div className="mt-4 pt-3 border-t border-gray-700">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-gray-400">Availability</span>
                  <span className={`font-medium ${employee.availability >= 50 ? 'text-green-400' : 'text-yellow-400'}`}>
                    {employee.availability}%
                  </span>
                </div>
                <div className="mt-1 h-2 bg-gray-700 rounded-full overflow-hidden">
                  <div
                    className={`h-full rounded-full ${employee.availability >= 50 ? 'bg-green-500' : 'bg-yellow-500'}`}
                    style={{ width: `${employee.availability}%` }}
                  />
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
