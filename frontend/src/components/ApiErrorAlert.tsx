import { Alert, AlertDescription } from './ui/alert';
import { ApiError, errorMessage } from '../lib/api/errors';
export function ApiErrorAlert({ error }: { error: unknown }) {
    return (
        <Alert variant="destructive">
            <AlertDescription>
                {errorMessage(error)}
                {error instanceof ApiError && error.correlationId && (
                    <span className="block text-xs select-all">Reference: {error.correlationId}</span>
                )}
            </AlertDescription>
        </Alert>
    );
}
