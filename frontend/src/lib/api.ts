const API_BASE = '/api';

export interface User {
  id: string;
  email: string;
  name: string;
  role: string;
}

export interface AuthResponse {
  success: boolean;
  token?: string;
  message?: string;
  user?: User;
}

export interface Employee {
  id: string;
  name: string;
  email: string;
  title?: string;
  jobTitle?: string;
  department: string;
  skills: { name: string; level: number }[];
  availability: number | string;
  yearsOfExperience: number;
  createdAt: string;
  updatedAt: string;
}

export interface RequiredSkill {
  name: string;
  minimumLevel: number;
  required: boolean;
}

export interface Project {
  id: string;
  name: string;
  description: string;
  requiredSkills: RequiredSkill[];
  teamSize: number;
  status: string | number;
  createdAt: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface MatchResult {
  employee: Employee;
  matchScore: number;
  baseMatchScore?: number;
  matchReasons: string[];
  bonusReasons?: string[];
  gaps: string[];
  skillMatches: unknown[];
  isFallbackCandidate?: boolean;
}

export interface MatchResponse {
  matches: MatchResult[];
  query: string;
  totalCandidates: number;
  analysis?: string;
  hasSufficientMatches?: boolean;
  recommendation?: string;
}

export interface ChatResponse {
  message: string;
  response: string;
  matches: MatchResult[];
  summary: string;
  totalCandidates: number;
  timestamp: string;
}

class ApiClient {
  private token: string | null = null;

  constructor() {
    this.token = localStorage.getItem('token');
  }

  setToken(token: string | null) {
    this.token = token;
    if (token) {
      localStorage.setItem('token', token);
    } else {
      localStorage.removeItem('token');
    }
  }

  getToken() {
    return this.token;
  }

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    if (this.token) {
      (headers as Record<string, string>)['Authorization'] = `Bearer ${this.token}`;
    }

    try {
      const response = await fetch(`${API_BASE}${endpoint}`, {
        ...options,
        headers,
      });

      // Only redirect on 401 if NOT on auth endpoints (login/register expect 401 for invalid credentials)
      if (response.status === 401 && !endpoint.startsWith('/auth/')) {
        this.setToken(null);
        window.location.href = '/login';
        throw new Error('Your session has expired. Please log in again.');
      }

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({
          error: 'Request Failed',
          message: `Request failed with status ${response.status}`
        }));

        // Handle specific error codes with user-friendly messages
        switch (response.status) {
          case 400:
            throw new Error(errorData.message || 'Invalid request. Please check your input.');
          case 404:
            throw new Error(errorData.message || 'The requested resource was not found.');
          case 503:
            throw new Error(errorData.message || 'Service is temporarily unavailable. Please try again.');
          case 500:
            throw new Error(errorData.message || 'An unexpected error occurred. Please try again later.');
          default:
            throw new Error(errorData.message || `Request failed with status ${response.status}`);
        }
      }

      if (response.status === 204) {
        return {} as T;
      }

      return response.json();
    } catch (error) {
      // Handle network errors
      if (error instanceof TypeError && error.message.includes('fetch')) {
        throw new Error('Network error. Please check your internet connection and try again.');
      }
      // Re-throw other errors
      throw error;
    }
  }

  // Auth
  async register(email: string, password: string, name: string): Promise<AuthResponse> {
    return this.request('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password, name }),
    });
  }

  async login(email: string, password: string): Promise<AuthResponse> {
    return this.request('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
  }

  async getCurrentUser(): Promise<User> {
    return this.request('/auth/me');
  }

  // Employees
  async getEmployees(page: number = 1, pageSize: number = 10): Promise<PaginatedResult<Employee>> {
    return this.request(`/employees?page=${page}&pageSize=${pageSize}`);
  }

  async getEmployee(id: string): Promise<Employee> {
    return this.request(`/employees/${id}`);
  }

  async createEmployee(employee: Partial<Employee>): Promise<Employee> {
    return this.request('/employees', {
      method: 'POST',
      body: JSON.stringify(employee),
    });
  }

  async updateEmployee(id: string, employee: Partial<Employee>): Promise<Employee> {
    return this.request(`/employees/${id}`, {
      method: 'PUT',
      body: JSON.stringify(employee),
    });
  }

  async deleteEmployee(id: string): Promise<void> {
    return this.request(`/employees/${id}`, { method: 'DELETE' });
  }

  // Projects
  async getProjects(page: number = 1, pageSize: number = 10): Promise<PaginatedResult<Project>> {
    return this.request(`/projects?page=${page}&pageSize=${pageSize}`);
  }

  async getProject(id: string): Promise<Project> {
    return this.request(`/projects/${id}`);
  }

  async createProject(project: Partial<Project>): Promise<Project> {
    return this.request('/projects', {
      method: 'POST',
      body: JSON.stringify(project),
    });
  }

  async updateProject(id: string, project: Partial<Project>): Promise<Project> {
    return this.request(`/projects/${id}`, {
      method: 'PUT',
      body: JSON.stringify(project),
    });
  }

  async deleteProject(id: string): Promise<void> {
    return this.request(`/projects/${id}`, { method: 'DELETE' });
  }

  // Matching
  async findMatches(query: string, teamSize: number = 5): Promise<MatchResponse> {
    return this.request('/match', {
      method: 'POST',
      body: JSON.stringify({ query, teamSize, availabilityRequired: true }),
    });
  }

  async findMatchesForProject(projectId: string, teamSize: number = 5): Promise<MatchResponse> {
    return this.request(`/projects/${projectId}/matches?teamSize=${teamSize}`);
  }

  // Chat
  async chat(message: string, history?: { role: string; content: string }[], teamSize?: number): Promise<ChatResponse> {
    return this.request('/chat', {
      method: 'POST',
      body: JSON.stringify({ message, history, teamSize }),
    });
  }
}

export const api = new ApiClient();
