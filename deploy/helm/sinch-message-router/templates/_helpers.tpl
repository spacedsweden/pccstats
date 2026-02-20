{{/*
Expand the name of the chart.
*/}}
{{- define "sinch-message-router.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this
(by the DNS naming spec). If release name contains chart name it will be used
as a full name.
*/}}
{{- define "sinch-message-router.fullname" -}}
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
{{- define "sinch-message-router.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "sinch-message-router.labels" -}}
helm.sh/chart: {{ include "sinch-message-router.chart" . }}
{{ include "sinch-message-router.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/part-of: sinch-message-router
{{- end }}

{{/*
Selector labels
*/}}
{{- define "sinch-message-router.selectorLabels" -}}
app.kubernetes.io/name: {{ include "sinch-message-router.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "sinch-message-router.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "sinch-message-router.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Return the image name
*/}}
{{- define "sinch-message-router.image" -}}
{{- $tag := default .Chart.AppVersion .Values.image.tag -}}
{{- printf "%s:%s" .Values.image.repository $tag }}
{{- end }}

{{/*
Return the secret name
*/}}
{{- define "sinch-message-router.secretName" -}}
{{- printf "%s-credentials" (include "sinch-message-router.fullname" .) }}
{{- end }}

{{/*
Return the configmap name
*/}}
{{- define "sinch-message-router.configmapName" -}}
{{- printf "%s-config" (include "sinch-message-router.fullname" .) }}
{{- end }}

{{/*
Mock server fullname
*/}}
{{- define "sinch-message-router.mockFullname" -}}
{{- printf "%s-mock" (include "sinch-message-router.fullname" .) | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Mock server labels
*/}}
{{- define "sinch-message-router.mockLabels" -}}
helm.sh/chart: {{ include "sinch-message-router.chart" . }}
{{ include "sinch-message-router.mockSelectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/part-of: sinch-message-router
{{- end }}

{{/*
Mock server selector labels
*/}}
{{- define "sinch-message-router.mockSelectorLabels" -}}
app.kubernetes.io/name: {{ include "sinch-message-router.name" . }}-mock
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/component: mock-server
{{- end }}

{{/*
OTel collector endpoint - returns sidecar or external endpoint
*/}}
{{- define "sinch-message-router.otelEndpoint" -}}
{{- if and .Values.otel.enabled .Values.otel.sidecar.enabled }}
{{- print "http://localhost:4317" }}
{{- else if .Values.otel.enabled }}
{{- .Values.otel.collectorEndpoint }}
{{- else }}
{{- print "" }}
{{- end }}
{{- end }}

{{/*
Checksum annotations for config and secret changes to trigger rolling restarts
*/}}
{{- define "sinch-message-router.checksumAnnotations" -}}
checksum/config: {{ include (print $.Template.BasePath "/configmap.yaml") . | sha256sum }}
{{- if .Values.secrets.create }}
checksum/secret: {{ include (print $.Template.BasePath "/secret.yaml") . | sha256sum }}
{{- end }}
{{- end }}
