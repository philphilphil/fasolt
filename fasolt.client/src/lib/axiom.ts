import { Axiom } from '@axiomhq/js'
import { Logger, AxiomJSTransport, ConsoleTransport } from '@axiomhq/logging'

const token = import.meta.env.VITE_AXIOM_TOKEN
const dataset = import.meta.env.VITE_AXIOM_DATASET

let logger: Logger | null = null

if (token && dataset) {
  const axiom = new Axiom({ token })
  logger = new Logger({
    transports: [
      new AxiomJSTransport({ axiom, dataset }),
      new ConsoleTransport(),
    ],
    args: {
      app: 'fasolt',
      env: import.meta.env.MODE,
    },
  })
}

export { logger }
