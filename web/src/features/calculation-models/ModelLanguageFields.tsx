import { VocabularyField } from '@/shared/form';

/** The formula language and attribution method of a model, as the create and edit sheets take them. */
export function ModelLanguageFields() {
  return (
    <>
      <VocabularyField
        name="formulaLanguageVersion"
        label="Formula language"
        vocabulary="formula_language_version"
        description="The grammar of the model's formulas and attribution expressions: 1.1 adds POWER(x, y), and 1.2 adds the model's parameters as @key, text compared by =, <> or IN, and YEARFRAC(day count). A model stays on the language its formulas need."
      />
      <VocabularyField
        name="attributionMethod"
        label="Attribution method"
        vocabulary="attribution_method"
        description="How a metric's change between two values splits into the effects of its drivers."
      />
    </>
  );
}
