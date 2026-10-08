import type { ConfigurationRequest } from '../../lib/api/types';
import { FormField } from '../../components/FormField';
import { Button } from '../../components/ui/button';
import { Label } from '../../components/ui/label';
export const emptyCatalog = (): ConfigurationRequest => ({
    authenticationEndpointId: '',
    allowInsecureLoopback: false,
    endpoints: [{ id: '', label: '', address: '' }],
});
export function CatalogEditor({
    value,
    onChange,
    errors = {},
    disabled = false,
}: {
    value: ConfigurationRequest;
    onChange: (value: ConfigurationRequest) => void;
    errors?: Record<string, string>;
    disabled?: boolean;
}) {
    function update(index: number, field: 'id' | 'label' | 'address', text: string) {
        const endpoints = value.endpoints.map((e, i) => (i === index ? { ...e, [field]: text } : e));
        onChange({
            ...value,
            endpoints,
            authenticationEndpointId:
                field === 'id' &&
                (value.authenticationEndpointId === value.endpoints[index].id || value.endpoints.length === 1)
                    ? text
                    : value.authenticationEndpointId,
        });
    }
    return (
        <fieldset disabled={disabled} className="space-y-5">
            <legend className="sr-only">Server connections</legend>
            {value.endpoints.map((endpoint, index) => (
                <div key={index} className="rounded-lg border bg-card p-4 space-y-4">
                    <div className="flex items-center justify-between">
                        <h3 className="text-sm font-medium">Server {index + 1}</h3>
                        <Button
                            type="button"
                            variant="ghost"
                            disabled={value.endpoints.length === 1}
                            aria-label={'Remove server ' + (index + 1)}
                            onClick={() => {
                                const endpoints = value.endpoints.filter((_, i) => i !== index);
                                onChange({
                                    ...value,
                                    endpoints,
                                    authenticationEndpointId:
                                        endpoint.id === value.authenticationEndpointId
                                            ? endpoints[0].id
                                            : value.authenticationEndpointId,
                                });
                            }}
                        >
                            Remove
                        </Button>
                    </div>
                    <div className="grid gap-4 sm:grid-cols-2">
                        <FormField
                            label="ID"
                            value={endpoint.id}
                            maxLength={64}
                            onChange={(e) => update(index, 'id', e.target.value)}
                            error={errors['endpoints.' + index + '.id']}
                        />
                        <FormField
                            label="Label"
                            value={endpoint.label}
                            maxLength={100}
                            onChange={(e) => update(index, 'label', e.target.value)}
                            error={errors['endpoints.' + index + '.label']}
                        />
                    </div>
                    <FormField
                        label="Address"
                        value={endpoint.address}
                        maxLength={2048}
                        placeholder="https://moongate.example:5001"
                        onChange={(e) => update(index, 'address', e.target.value)}
                        error={errors['endpoints.' + index + '.address']}
                    />
                </div>
            ))}
            <Button
                type="button"
                variant="outline"
                disabled={value.endpoints.length >= 16}
                onClick={() => onChange({ ...value, endpoints: [...value.endpoints, { id: '', label: '', address: '' }] })}
            >
                Add server
            </Button>
            <div className="space-y-2">
                <Label htmlFor="authentication-server">Authentication server</Label>
                <select
                    id="authentication-server"
                    className="native-select"
                    value={value.authenticationEndpointId}
                    onChange={(e) => onChange({ ...value, authenticationEndpointId: e.target.value })}
                >
                    <option value="">Select a Login or Standalone server</option>
                    {value.endpoints
                        .filter((e) => e.id)
                        .map((e) => (
                            <option key={e.id} value={e.id}>
                                {e.label || e.id}
                            </option>
                        ))}
                </select>
                {errors.authenticationEndpointId && (
                    <p className="text-destructive text-sm">{errors.authenticationEndpointId}</p>
                )}
            </div>
            <label className="flex items-start gap-3 text-sm">
                <input
                    type="checkbox"
                    checked={value.allowInsecureLoopback}
                    onChange={(e) => onChange({ ...value, allowInsecureLoopback: e.target.checked })}
                />
                <span>
                    Allow insecure loopback in Development
                    <span className="block text-muted-foreground mt-1">
                        Only local loopback addresses. Production always requires HTTPS.
                    </span>
                </span>
            </label>
            {errors.endpoints && <p className="text-destructive">{errors.endpoints}</p>}
        </fieldset>
    );
}
