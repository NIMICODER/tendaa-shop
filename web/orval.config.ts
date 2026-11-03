import { defineConfig } from 'orval'

// Generates the typed API client + TanStack Query hooks from the backend's OpenAPI
// document. Run manually against a locally running dev server — never part of
// `npm run build` or CI, so those stay independent of a live backend:
//
//   dotnet run --project src/Tendaa.Web   # serves /openapi/v1.json in Development
//   npm run api:generate --prefix web
export default defineConfig({
  tendaa: {
    input: {
      target: 'http://localhost:5093/openapi/v1.json',
    },
    output: {
      target: './src/api/generated/endpoints.ts',
      schemas: './src/api/generated/models',
      client: 'react-query',
      mock: false,
      clean: true,
    },
  },
})
