﻿namespace Foo {
	export enum Status {
		Striving = 5,
		Endangered,
		Instinct
	}

	export interface AnimalBase {
		name?: string;
		legs?: number;
		status?: Status;
	}

	export interface FelineBase extends AnimalBase {
		whiskers?: number;
	}

	export interface PatheraBase extends AnimalBase {
		stealth?: number;
	}

	export class LionBase implements FelineBase, PatheraBase {
		name: string;
		legs: number;
		status: Status;
		kills: number;
		whiskers: number;
		stealth: number;
	}
}