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
  availability: number;
  yearsOfExperience: number;
  createdAt: string;
  updatedAt: string;
}

export interface Project {
  id: string;
  name: string;
  description: string;
  requiredSkills: string[];
  teamSize: number;
  status: string;
  createdAt: string;
}

export interface MatchResult {
  employee: Employee;
  matchScore: number;
  matchReasons: string[];
  gaps: string[];
  skillMatches: unknown[];
}

export interface MatchResponse {
  matches: MatchResult[];
  query: string;
  totalCandidates: number;
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

    const response = await fetch(`${API_BASE}${endpoint}`, {
      ...options,
      headers,
    });

    if (response.status === 401) {
      this.setToken(null);
      window.location.href = '/login';
      throw new Error('Unauthorized');
    }

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Request failed' }));
      throw new Error(error.message || 'Request failed');
    }

    if (response.status === 204) {
      return {} as T;
    }

    return response.json();
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
  async getEmployees(): Promise<Employee[]> {
    return this.request('/employees');
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
  async getProjects(): Promise<Project[]> {
    return this.request('/projects');
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
