declare enum Status {
	Striving = 5,
	Endangered,
	Instinct
}

interface Animal {
	name?: string;
	legs?: number;
	status?: Status;
}

interface Feline extends Animal {
	whiskers?: number;
}

interface Pathera extends Animal {
	stealth?: number;
}

interface Lion extends Feline, Pathera {
	
	legs?: number;
	status?: Status;
	kills?: number;
	whiskers?: number;
	stealth?: number;
}


interface Predator {
	name?: string;
}

interface Skill<T> {
}

interface AfricanLion extends Predator, Skill<any> {
	name?: string;
	region?: string;
}