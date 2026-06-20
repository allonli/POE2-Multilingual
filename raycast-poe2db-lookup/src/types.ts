export type Poe2DbLanguage = "cn" | "tw" | "us";

export type AutocompleteItem = {
  label: string;
  value: string;
  desc?: string | null;
  class?: string | null;
};

export type NameRecord = {
  value: string;
  cnLabel: string;
  twLabel: string;
  usLabel: string;
  type: string;
  className: string;
};

export type OutputChoice = {
  label: string;
  text: string;
  url: string;
};
