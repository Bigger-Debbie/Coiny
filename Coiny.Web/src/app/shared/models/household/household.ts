export interface Household {
  id: number;
  name: string;
  role: 'Owner' | 'Member';
}

export interface HouseholdMember { id: number; firstName: string | null; email: string; role: 'Owner' | 'Member'; isCurrentUser: boolean; }
