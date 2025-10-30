{{/*
Expand the name of the chart.
*/}}
{{- define "business-process-agents.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "business-process-agents.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "business-process-agents.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "business-process-agents.labels" -}}
helm.sh/chart: {{ include "business-process-agents.chart" . }}
{{ include "business-process-agents.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "business-process-agents.selectorLabels" -}}
app.kubernetes.io/name: {{ include "business-process-agents.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "business-process-agents.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "business-process-agents.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Control Plane labels
*/}}
{{- define "business-process-agents.controlPlane.labels" -}}
{{ include "business-process-agents.labels" . }}
app.kubernetes.io/component: control-plane
{{- end }}

{{/*
Control Plane selector labels
*/}}
{{- define "business-process-agents.controlPlane.selectorLabels" -}}
{{ include "business-process-agents.selectorLabels" . }}
app.kubernetes.io/component: control-plane
{{- end }}

{{/*
Node Runtime labels
*/}}
{{- define "business-process-agents.nodeRuntime.labels" -}}
{{ include "business-process-agents.labels" . }}
app.kubernetes.io/component: node-runtime
{{- end }}

{{/*
Node Runtime selector labels
*/}}
{{- define "business-process-agents.nodeRuntime.selectorLabels" -}}
{{ include "business-process-agents.selectorLabels" . }}
app.kubernetes.io/component: node-runtime
{{- end }}

{{/*
Admin UI labels
*/}}
{{- define "business-process-agents.adminUI.labels" -}}
{{ include "business-process-agents.labels" . }}
app.kubernetes.io/component: admin-ui
{{- end }}

{{/*
Admin UI selector labels
*/}}
{{- define "business-process-agents.adminUI.selectorLabels" -}}
{{ include "business-process-agents.selectorLabels" . }}
app.kubernetes.io/component: admin-ui
{{- end }}

{{/*
PostgreSQL connection string
*/}}
{{- define "business-process-agents.postgresql.connectionString" -}}
{{- if .Values.postgresql.enabled -}}
Host={{ include "business-process-agents.fullname" . }}-postgresql:{{ .Values.postgresql.service.port }};Database={{ .Values.postgresql.auth.database }};Username={{ .Values.postgresql.auth.username }};Password={{ .Values.postgresql.auth.password }}
{{- end -}}
{{- end -}}

{{/*
Redis connection string
*/}}
{{- define "business-process-agents.redis.connectionString" -}}
{{- if .Values.redis.enabled -}}
{{ include "business-process-agents.fullname" . }}-redis:{{ .Values.redis.service.port }}
{{- end -}}
{{- end -}}

{{/*
NATS connection string
*/}}
{{- define "business-process-agents.nats.connectionString" -}}
{{- if .Values.nats.enabled -}}
nats://{{ include "business-process-agents.fullname" . }}-nats:{{ .Values.nats.service.clientPort }}
{{- end -}}
{{- end -}}
