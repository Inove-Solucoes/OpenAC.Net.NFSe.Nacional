using OpenAC.Net.Core.Logging;
using OpenAC.Net.NFSe.Nacional.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Common.Types;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenAC.Net.NFSe.Nacional.Webservice.VilaVelhaSoap
{
    /// <summary>
    /// Provedor para o Webservice SOAP de Vila Velha.
    /// Autor: Lucas Giovani de Paula Salgado
    /// Contato: lucas@salgado.dev
    /// Data: 10/03/2026
    /// </summary>
    public class VilaVelhaSoapWebservice : NFSeWebserviceBase
    {
        #region Construtor

        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="VilaVelhaSoapWebservice"/>
        /// </summary>
        /// <param name="configuracaoNFSe">Configuração da NFSe.</param>
        /// <param name="serviceInfo">Informações do serviço</param>"

        public VilaVelhaSoapWebservice(ConfiguracaoNFSe configuracaoNFSe, NFSeServiceInfo serviceInfo)
            : base(configuracaoNFSe, serviceInfo)
        { Console.WriteLine("Provider Vila Velha Carregado."); }

        #endregion

        #region Métodos

        #region DANFSe

        /// <summary>
        /// Retorna o DANFSe de uma NFS-e a partir de sua chave de acesso.
        /// </summary>
        /// <param name="chave">Chave de acesso da NFS-e.</param>
        /// <returns>Array de bytes contendo o DANFSe.</returns>
        public override async Task<byte[]> DownloadDANFSeAsync(string chave)
        {
            throw new System.NotImplementedException();
        }

        #endregion DANFSe

        #region DFe

        /// <summary>
        /// Distribui os DF-e para contribuintes relacionados à NFS-e.
        /// </summary>
        /// <param name="nsu">Número NSU.</param>
        /// <returns>Resposta da consulta contendo os DF-e.</returns>
        public override async Task<NFSeResponse<RespostaConsultaDFe>> ConsultaNsuAsync(int nsu)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Distribui os DF-e vinculados à chave de acesso informada.
        /// </summary>
        /// <param name="chave">Chave de acesso da NFS-e.</param>
        /// <returns>Resposta da consulta contendo os DF-e.</returns>
        public override async Task<NFSeResponse<RespostaConsultaDFe>> ConsultaChaveAsync(string chave)
        {
            throw new System.NotImplementedException();
        }

        #endregion DFe

        #region DPS

        /// <summary>
        /// Retorna a chave de acesso da NFS-e a partir do identificador do DPS.
        /// </summary>
        /// <param name="id">Identificação do DPS.</param>
        /// <returns>Resposta da consulta contendo a chave de acesso.</returns>

        public override async Task<NFSeResponse<RespostaConsultaChaveDps>> ConsultaChaveDpsAsync(string id)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Verifica se uma NFS-e foi emitida a partir do Id do DPS.
        /// </summary>
        /// <param name="id">Identificação do DPS.</param>
        /// <returns>True se existir, caso contrário false.</returns>
        public override async Task<bool> ConsultaExisteDpsAsync(string id)
        {
            throw new System.NotImplementedException();
        }

        #endregion DPS

        #region Eventos

        /// <summary>
        /// Recepciona o Pedido de Registro de Evento e gera Eventos de NFS-e, crédito, débito e apuração.
        /// </summary>
        /// <param name="evento">Evento a ser enviado.</param>
        /// <returns>Resposta do envio do evento.</returns>
        public override async Task<NFSeResponse<RespostaEnvioEvento>> EnviarEventoAsync(PedidoRegistroEvento evento)
        {
            throw new System.NotImplementedException();
        }

        #endregion Eventos

        #region NFS-e

        /// <summary>
        /// Recepciona a DPS e gera a NFS-e de forma síncrona.
        /// </summary>
        /// <param name="dps">DPS a ser enviada.</param>
        /// <returns>Resposta do envio da DPS.</returns>
        public override async Task<NFSeResponse<RespostaEnvioDps>> EnviarAsync(Dps dps)
        {
            AjustarDpsVilaVelha(ref dps);

            var xmlDps = Uteis.GerarXmlDpsSemAssinatura(dps);

            ValidarSchema(SchemaNFSe.DPS, xmlDps, dps.Versao);

            var xmlComNFse = AdicionarNFSeVilaVelha(xmlDps, dps);

            xmlComNFse = Uteis.AssinarUsandoOCampoInfNFSe(xmlComNFse, Configuracao);

            var soapXml = Uteis.MontarSoap(xmlComNFse, "NotaFiscalNacionalGerar");

            var documento = dps.Informacoes.Prestador.CPF ?? dps.Informacoes.Prestador.CNPJ;

            // salva DPS xml
            GravarDpsEmDisco(
                xmlDps,
                $"{dps.Informacoes.NumeroDps:000000}_dps.xml",
                documento,
                dps.Informacoes.DhEmissao.DateTime
            );

            var url = ServiceInfo[Configuracao.WebServices.Ambiente][Common.Types.TipoUrl.Enviar];

            this.Log().Debug($"Webservice Vila Velha: [Enviar][Envio] - {soapXml}");

            // salva envio SOAP
            GravarArquivoEmDisco(
                soapXml,
                $"Enviar-{dps.Informacoes.NumeroDps:000000}-env.xml",
                documento
            );

            var content = new StringContent(soapXml, Encoding.UTF8, "text/xml");
            content.Headers.Clear();
            content.Headers.Add("Content-Type", "text/xml; charset=utf-8");

            var httpResponse = await SendAsync(content, HttpMethod.Post, url);

            var respostaXml = await httpResponse.Content.ReadAsStringAsync();

            this.Log().Debug($"Webservice Vila Velha: [Enviar][Resposta] - {respostaXml}");

            // salva resposta SOAP
            GravarArquivoEmDisco(
                respostaXml,
                $"Enviar-{dps.Informacoes.NumeroDps:000000}-resp.xml",
                documento
            );

            var retorno = NFSeResponse<RespostaEnvioDps>.Create(
                xmlDps,
                soapXml,
                respostaXml,
                httpResponse.IsSuccessStatusCode
            );

            // se retorno contiver NFSe
            if (retorno.Sucesso && retorno.JsonRetorno.Contains("PROCESSADO_COM_SUCESSO"))
            {
                GravarNFSeEmDisco(
                    retorno.JsonRetorno,
                    $"{dps.Informacoes.NumeroDps:000000}_nfse.xml",
                    documento,
                    dps.Informacoes.DhEmissao.DateTime
                );
            }

            return retorno;
        }

        #endregion NFS-e

        #endregion Métodos

        #region Métodos Auxiliares

        

        private static void AjustarDpsVilaVelha(ref Dps dps)
        {
            dps.Informacoes.LocalidadeEmitente = "3205200";
            dps.Informacoes.Servico.Localidade.CodMunicipioPrestacao = "3205200";
        }

        private static string AdicionarNFSeVilaVelha(string xmlEnvio, Dps dps)
        {
            xmlEnvio = xmlEnvio.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "").Trim();

            var dpsElement = XElement.Parse(xmlEnvio);

            var assinaturaDps = dpsElement.Elements().FirstOrDefault(x => x.Name.LocalName == "Signature");
            assinaturaDps?.Remove();

            string nfseId = "NFS"
                + "3205200"
                + "1"
                + (string.IsNullOrEmpty(dps.Informacoes.Prestador.CPF) ? "2" : "1")
                + (string.IsNullOrEmpty(dps.Informacoes.Prestador.CPF)
                    ? Uteis.SomenteDigitos(dps.Informacoes.Prestador.CNPJ).PadLeft(14, '0')
                    : Uteis.SomenteDigitos(dps.Informacoes.Prestador.CPF).PadLeft(14, '0'))
                + dps.Informacoes.NumeroDps.ToString().PadLeft(13, '0')
                + dps.Informacoes.Competencia.ToString("yyMM")
                + dps.Informacoes.NumeroDps.ToString().PadLeft(9, '0');

            nfseId += Uteis.CalcularDvModulo11(nfseId);

            XNamespace ns = "http://www.sped.fazenda.gov.br/nfse";

            var emit = new XElement(ns + "emit");

            if (string.IsNullOrEmpty(dps.Informacoes.Prestador.CPF))
                emit.Add(new XElement(ns + "CNPJ", Uteis.SomenteDigitos(dps.Informacoes.Prestador.CNPJ)));
            else
                emit.Add(new XElement(ns + "CPF", Uteis.SomenteDigitos(dps.Informacoes.Prestador.CPF)));

            emit.Add(new XElement(ns + "IM", dps.Informacoes.Prestador.InscricaoMunicipal));

            var enderNac = MontarEnderNacDoEmit(ns, dpsElement);
            if (enderNac != null)
                emit.Add(enderNac);

            var nfse = new XDocument(
                new XElement(ns + "NFSe",
                    new XAttribute("versao", "1.00"),
                    new XElement(ns + "infNFSe",
                        new XAttribute("Id", nfseId),
                        new XElement(ns + "xLocEmi", "Vila Velha"),
                        new XElement(ns + "xLocPrestacao", "Vila Velha"),
                        new XElement(ns + "nNFSe", "0"),
                        new XElement(ns + "cLocIncid", "3205200"),
                        new XElement(ns + "xLocIncid", "Vila Velha"),
                        new XElement(ns + "xTribNac", "Serviços de registros públicos, cartorários e notariais."),
                        new XElement(ns + "verAplic", "SilTecnologia_v1.00"),
                        new XElement(ns + "ambGer", "1"),
                        new XElement(ns + "tpEmis", "2"),
                        new XElement(ns + "cStat", "100"),
                        new XElement(ns + "dhProc", dps.Informacoes.DhEmissao.ToString("yyyy-MM-ddTHH:mm:sszzz")),
                        new XElement(ns + "nDFSe", dps.Informacoes.NumeroDps.ToString()),
                        emit,
                        new XElement(ns + "valores",
                            new XElement(ns + "vBC", Uteis.FormatarValorPadraoNFSe(dps.Informacoes.Valores.ValoresServico.Valor)),
                            new XElement(ns + "pAliqAplic", Uteis.FormatarValorPadraoNFSe(dps.Informacoes.Valores.Tributos.Total.PorcentagemTotal.TotalMunicipal)),
                            new XElement(ns + "vISSQN", Uteis.FormatarValorPadraoNFSe(dps.Informacoes.Valores.ValoresServico.Valor * (dps.Informacoes.Valores.Tributos.Total.PorcentagemTotal.TotalMunicipal/100m))),
                            new XElement(ns + "vTotalRet", "0.00"),
                            new XElement(ns + "vLiq", Uteis.FormatarValorPadraoNFSe(dps.Informacoes.Valores.ValoresServico.Valor))
                        ),
                        dpsElement
                    )
                )
            );

            return nfse.ToString();
        }

        private static XElement? MontarEnderNacDoEmit(XNamespace ns, XElement dpsElement)
        {
            var prest = dpsElement.Descendants().FirstOrDefault(x => x.Name.LocalName == "prest");
            var end = prest?.Elements().FirstOrDefault(x => x.Name.LocalName == "end");

            if (end == null)
                return null;

            string? xLgr = end.Elements().FirstOrDefault(x => x.Name.LocalName == "xLgr")?.Value;
            string? nro = end.Elements().FirstOrDefault(x => x.Name.LocalName == "nro")?.Value;
            string? xCpl = end.Elements().FirstOrDefault(x => x.Name.LocalName == "xCpl")?.Value;
            string? xBairro = end.Elements().FirstOrDefault(x => x.Name.LocalName == "xBairro")?.Value;

            // Como no seu DPS de prestador não existe cMun/UF/CEP dentro de <end>,
            // você precisa preencher manualmente ou puxar de outra fonte.
            var cMun = "3205200";
            var uf = "ES";
            var cep = "29100000"; // troque pelo CEP real se tiver

            var enderNac = new XElement(ns + "enderNac",
                new XElement(ns + "xLgr", xLgr ?? "NAO INFORMADO"),
                new XElement(ns + "nro", string.IsNullOrWhiteSpace(nro) ? "0" : nro),
                new XElement(ns + "xBairro", xBairro ?? "NAO INFORMADO"),
                new XElement(ns + "cMun", cMun ?? "NAO INFORMADO"),
                new XElement(ns + "UF", uf ?? "NAO INFORMADO"),
                new XElement(ns + "CEP", cep ?? "NAO INFORMADO")
            );

            if (!string.IsNullOrWhiteSpace(xCpl))
                enderNac.Add(new XElement(ns + "xCpl", xCpl));

            return enderNac;
        }

        #endregion Métodos Auxiliares
    }
}
