import { mkdir, writeFile } from 'node:fs/promises'

const outputDirectory = new URL('../dist/server/', import.meta.url)
const workerSource = `const worker = {
  async fetch(request, env) {
    const response = await env.ASSETS.fetch(request)
    const acceptsHtml = request.headers.get('accept')?.includes('text/html')

    if (response.status === 404 && request.method === 'GET' && acceptsHtml) {
      const indexUrl = new URL('/index.html', request.url)
      return env.ASSETS.fetch(new Request(indexUrl, request))
    }

    return response
  }
}

export default worker
`

await mkdir(outputDirectory, { recursive: true })
await writeFile(new URL('index.js', outputDirectory), workerSource)
