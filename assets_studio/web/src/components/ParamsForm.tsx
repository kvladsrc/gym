// A form generated from a task's JSON Schema of parameters (ADR-001: a flat
// object of scalars or enumerations, each with a default). Parameters marked
// "x-primary" (ADR-003) are shown up front, the rest under "advanced".
type Schema = Record<string, unknown>;
export type Params = Record<string, unknown>;

interface Property {
  type?: string;
  enum?: unknown[];
  default?: unknown;
  title?: string;
  description?: string;
  minimum?: number;
  maximum?: number;
  exclusiveMinimum?: number;
  exclusiveMaximum?: number;
  "x-primary"?: boolean;
}

export const isPrimary = (prop: Property) => prop["x-primary"] === true;

export function properties(schema: Schema): [string, Property][] {
  const props = schema.properties;
  return props && typeof props === "object"
    ? Object.entries(props as Record<string, Property>)
    : [];
}

export function defaults(schema: Schema): Params {
  return Object.fromEntries(
    properties(schema).map(([name, prop]) => [name, prop.default]),
  );
}

/** The number typed into a field, or null if it is not a valid value. */
export function parseNumber(text: string, prop: Property): number | null {
  // Decimal notation with an optional exponent: 2, 0.25, .5, 1e-4.
  if (!/^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(text.trim())) return null;
  const number = Number(text);
  if (prop.type === "integer" && !Number.isInteger(number)) return null;
  if (prop.minimum !== undefined && number < prop.minimum) return null;
  if (prop.maximum !== undefined && number > prop.maximum) return null;
  if (prop.exclusiveMinimum !== undefined && number <= prop.exclusiveMinimum)
    return null;
  if (prop.exclusiveMaximum !== undefined && number >= prop.exclusiveMaximum)
    return null;
  return number;
}

function range(prop: Property) {
  const low =
    prop.minimum !== undefined
      ? `от ${prop.minimum}`
      : prop.exclusiveMinimum !== undefined
        ? `больше ${prop.exclusiveMinimum}`
        : "";
  const high =
    prop.maximum !== undefined
      ? `до ${prop.maximum}`
      : prop.exclusiveMaximum !== undefined
        ? `меньше ${prop.exclusiveMaximum}`
        : "";
  const kind = prop.type === "integer" ? "целое число" : "число";
  return [kind, low, high].filter(Boolean).join(" ");
}

/** Raw texts of number fields as typed ("0.", "" …), kept in the draft so the
 * shown text and its validity have a single source. */
export type Texts = Record<string, string>;

/** Number fields whose typed text is not a valid value. */
export function invalidParams(
  schema: Schema,
  texts: Texts,
): [string, Property][] {
  return properties(schema).filter(([name, prop]) => {
    const text = texts[name];
    return text !== undefined && parseNumber(text, prop) === null;
  });
}

function NumberField({
  label,
  prop,
  text,
  onInput,
}: {
  label: string;
  prop: Property;
  text: string;
  onInput: (text: string) => void;
}) {
  const valid = parseNumber(text, prop) !== null;
  return (
    <label class="field">
      <span>{label}</span>
      <input
        type="text"
        inputMode={prop.type === "integer" ? "numeric" : "decimal"}
        class={valid ? "" : "invalid"}
        aria-invalid={!valid}
        value={text}
        onInput={(event) => onInput(event.currentTarget.value)}
      />
      {!valid && <span class="hint error">Нужно {range(prop)}</span>}
    </label>
  );
}

function Field({
  name,
  prop,
  value,
  text,
  onChange,
  onText,
}: {
  name: string;
  prop: Property;
  value: unknown;
  text: string | undefined;
  onChange: (value: unknown) => void;
  onText: (text: string) => void;
}) {
  const label = prop.description ?? prop.title ?? name;
  if (prop.enum) {
    return (
      <label class="field">
        <span>{label}</span>
        <select
          value={String(value)}
          onChange={(event) => {
            const chosen = prop.enum?.find(
              (option) => String(option) === event.currentTarget.value,
            );
            onChange(chosen);
          }}
        >
          {prop.enum.map((option) => (
            <option key={String(option)} value={String(option)}>
              {String(option)}
            </option>
          ))}
        </select>
      </label>
    );
  }
  if (prop.type === "boolean") {
    return (
      <label class="check">
        <input
          type="checkbox"
          checked={Boolean(value)}
          onChange={(event) => onChange(event.currentTarget.checked)}
        />
        {label}
      </label>
    );
  }
  if (prop.type === "integer" || prop.type === "number") {
    return (
      <NumberField
        label={label}
        prop={prop}
        text={text ?? (typeof value === "number" ? String(value) : "")}
        onInput={onText}
      />
    );
  }
  return (
    <label class="field">
      <span>{label}</span>
      <input
        type="text"
        value={typeof value === "string" ? value : ""}
        onInput={(event) => onChange(event.currentTarget.value)}
      />
    </label>
  );
}

/** Fully controlled by the draft: ``onChange(values, texts)``. A number field
 * updates its value only while its text is valid; the last valid value stays. */
export function ParamsForm({
  schema,
  values,
  texts,
  primary,
  onChange,
}: {
  schema: Schema;
  values: Params;
  texts: Texts;
  /** Only the primary parameters (true) or only the others (false). */
  primary: boolean;
  onChange: (values: Params, texts: Texts) => void;
}) {
  return (
    <>
      {properties(schema)
        .filter(([, prop]) => isPrimary(prop) === primary)
        .map(([name, prop]) => (
          <Field
            key={name}
            name={name}
            prop={prop}
            value={values[name]}
            text={texts[name]}
            onChange={(value) => onChange({ ...values, [name]: value }, texts)}
            onText={(text) => {
              const number = parseNumber(text, prop);
              onChange(
                number === null ? values : { ...values, [name]: number },
                {
                  ...texts,
                  [name]: text,
                },
              );
            }}
          />
        ))}
    </>
  );
}
