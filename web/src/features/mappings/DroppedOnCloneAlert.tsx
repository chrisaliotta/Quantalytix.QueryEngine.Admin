import { TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';

export interface DroppedOnCloneAlertProps {
  /** What the clone left out, such as `v3 left out 2 of the cloned entries`. */
  title: string;
  /** Why, such as `No member of the pinned version has the member key of these source keys:`. */
  description: string;
  /** The rows the clone left out, as text. */
  items: string[];
  onDismiss: () => void;
}

/** Notice after a clone that could not carry every row over: it lists the rows it left out until dismissed. */
export function DroppedOnCloneAlert({ title, description, items, onDismiss }: DroppedOnCloneAlertProps) {
  return (
    <Alert data-slot="dropped-on-clone-alert">
      <TriangleAlert />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p>{description}</p>
        <ul className="max-h-40 w-full overflow-y-auto font-mono text-xs">
          {items.map((item, index) => (
            <li key={`${index}:${item}`}>{item}</li>
          ))}
        </ul>
        <Button variant="outline" size="xs" onClick={onDismiss}>
          Dismiss
        </Button>
      </AlertDescription>
    </Alert>
  );
}
