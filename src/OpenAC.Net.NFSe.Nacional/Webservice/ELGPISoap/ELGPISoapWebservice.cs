using OpenAC.Net.Core.Logging;
using OpenAC.Net.NFSe.Nacional.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Common.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace OpenAC.Net.NFSe.Nacional.Webservice.ELGPISoap
{
    /// <summary>
    /// Provedor para o Webservice ELGPI Soap.
    /// Este é o provedor de 27 municípios
    /// Autor: Lucas Giovani de Paula Salgado
    /// Contato: lucas@salgado.dev
    /// Data: 13/03/2026
    /// </summary>
    public class ELGPISoapWebservice : NFSeWebserviceBase
    {
        #region Constructor
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="ELGPISoapWebservice"/>
        /// </summary>
        /// <param name="configuracaoNFSe">Configuração da NFSe.</param>
        /// <param name="serviceInfo">Informações do serviço</param>
        public ELGPISoapWebservice(ConfiguracaoNFSe configuracaoNFSe, NFSeServiceInfo serviceInfo)
            : base(configuracaoNFSe, serviceInfo)
        { Console.WriteLine("Provider EL GPI Soap Caregado."); }

        #endregion Constructor

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

        // <summary>
        /// Recepciona a DPS e gera a NFS-e de forma síncrona.
        /// </summary>
        /// <param name="dps">DPS a ser enviada.</param>
        /// <returns>Resposta do envio da DPS.</returns>
        public override async Task<NFSeResponse<RespostaEnvioDps>> EnviarAsync(Dps dps)
        {
            var xmlGerarNfse = GerarNfseEnvioAbrasf(dps);

            ValidarSchema(SchemaNFSe.AbrasfRPS, xmlGerarNfse, VersaoNFSe.Ve204);

            var documento = dps.Informacoes.Prestador.CPF ?? dps.Informacoes.Prestador.CNPJ;
            GravarArquivoEmDisco(xmlGerarNfse, $"Enviar-{dps.Informacoes.NumeroDps:000000}-rps.xml", documento);

            var soapEnvelope = MontarEnvelopeSoap(xmlGerarNfse);

            this.Log().Debug($"Webservice EL GPI Soap: [Enviar][Envio] - {soapEnvelope}");
            GravarArquivoEmDisco(soapEnvelope, $"Enviar-{dps.Informacoes.NumeroDps:000000}-env.xml", documento);

            var url = ServiceInfo[Configuracao.WebServices.Ambiente][TipoUrl.Enviar];

            var content = new StringContent(soapEnvelope, new UTF8Encoding(false), "text/xml");
            var httpResponse = await SendSoap(content, HttpMethod.Post, url);
            var strResponse = await httpResponse.Content.ReadAsStringAsync();

            this.Log().Debug($"Webservice EL GPI Soap: [Enviar][Resposta] - {strResponse}");
            GravarArquivoEmDisco(strResponse, $"Enviar-{dps.Informacoes.NumeroDps:000000}-resp.xml", documento);

            var retorno = ParseRespostaEnvioSoap(xmlGerarNfse, soapEnvelope, strResponse, httpResponse.IsSuccessStatusCode);

            if (retorno.Sucesso && !string.IsNullOrEmpty(retorno.Resultado?.XmlNFSe))
                GravarNFSeEmDisco(retorno.Resultado.XmlNFSe, $"{dps.Informacoes.NumeroDps:000000}_nfse.xml", documento, dps.Informacoes.DhEmissao.DateTime);

            return retorno;
        }

        #endregion NFS-e

        #region Métodos Auxiliares

        private static string GerarNfseEnvioAbrasf(Dps dps)
        {
            XNamespace ns = "http://www.abrasf.org.br/nfse.xsd";
            var inf = dps.Informacoes;

            MunicipioNacional tomadorMunicipio = (MunicipioNacional)inf.Tomador.Endereco.Municipio;

            var root = new XElement(ns + "GerarNfseEnvio",
                new XAttribute(XNamespace.Xmlns + "ns1", ns),
                new XElement(ns + "Rps",
                    new XElement(ns + "InfDeclaracaoPrestacaoServico",
                        new XAttribute("Id", inf.NumeroDps),

                        new XElement(ns + "Rps",
                            new XAttribute("Id", inf.NumeroDps),
                            new XElement(ns + "IdentificacaoRps",
                                new XElement(ns + "Numero", inf.NumeroDps),
                                new XElement(ns + "Serie", "NFE"),
                                new XElement(ns + "Tipo", "1")
                            ),
                            new XElement(ns + "DataEmissao", inf.DhEmissao.ToString("yyyy-MM-dd")),
                            new XElement(ns + "Status", "1")
                        ),

                        new XElement(ns + "Competencia", inf.Competencia.ToString("yyyy-MM-dd")),

                        new XElement(ns + "Servico",
                            new XElement(ns + "Valores",
                                new XElement(ns + "ValorServicos", Uteis.FormatarValorPadraoNFSe(inf.Valores.ValoresServico.Valor)),
                                new XElement(ns + "ValorDeducoes", Uteis.FormatarValorPadraoNFSe(inf.Valores.ValoresDeducaoReducao != null ? inf.Valores.ValoresDeducaoReducao.Valor.Value : 0.00m)),
                                new XElement(ns + "ValorPis" , "0.00"),
                                new XElement(ns + "ValorCofins", "0.00"),
                                new XElement(ns + "ValorInss", "0.00"),
                                new XElement(ns + "ValorIr", "0.00"),
                                new XElement(ns + "ValorCsll", "0.00"),
                                new XElement(ns + "OutrasRetencoes", "0.00"),
                                new XElement(ns + "ValorIss", Uteis.FormatarValorPadraoNFSe(
                                    (inf.Valores.ValoresServico.Valor - (inf.Valores.ValoresDeducaoReducao != null ? inf.Valores.ValoresDeducaoReducao.Valor.Value : 0.00m))
                                    * (inf.Valores.Tributos.Total.PorcentagemTotal.TotalMunicipal / 100m))),
                                new XElement(ns + "Aliquota", Uteis.FormatarValorPadraoNFSe(
                                    inf.Valores.Tributos.Total.PorcentagemTotal.TotalMunicipal)),
                                new XElement(ns + "DescontoIncondicionado", "0"),
                                new XElement(ns + "DescontoCondicionado", "0")
                            ),
                            new XElement(ns + "IssRetido", inf.Valores.Tributos.Municipal.TipoRetencaoISSQN == 0 ? 2 : 1),
                            (int)inf.Valores.Tributos.Municipal.TipoRetencaoISSQN == 0 ? "" : new XElement(ns + "ResponsavelRetencao", (int)inf.Valores.Tributos.Municipal.TipoRetencaoISSQN),
                            new XElement(ns + "ItemListaServico", inf.Servico.Informacoes.CodTributacaoMunicipio),
                            new XElement(ns + "CodigoTributacaoMunicipio", inf.Servico.Informacoes.CodTributacaoMunicipio),
                            new XElement(ns + "CodigoServicoNacional", inf.Servico.Informacoes.CodTributacaoNacional),
                            new XElement(ns + "CodigoNbs", inf.Servico.Informacoes.CodNBS),
                            new XElement(ns + "Discriminacao", Regex.Replace(inf.Servico.Informacoes.Descricao, @"\s+", " ").Trim()),
                            new XElement(ns + "CodigoMunicipio", inf.Servico.Localidade.CodMunicipioPrestacao),
                            new XElement(ns + "ExigibilidadeISS", "1"),
                            new XElement(ns + "MunicipioIncidencia", inf.Servico.Localidade.CodMunicipioPrestacao)
                        ),

                        new XElement(ns + "Prestador",
                            new XElement(ns + "CpfCnpj",
                                string.IsNullOrEmpty(inf.Prestador.CNPJ)
                                    ? new XElement(ns + "Cpf", inf.Prestador.CPF)
                                    : new XElement(ns + "Cnpj", inf.Prestador.CNPJ)
                            ),
                            new XElement(ns + "InscricaoMunicipal", inf.Prestador.InscricaoMunicipal)
                        ),

                        new XElement(ns + "TomadorServico",
                            new XElement(ns + "IdentificacaoTomador",
                                new XElement(ns + "CpfCnpj",
                                    string.IsNullOrEmpty(inf.Tomador.CNPJ)
                                        ? new XElement(ns + "Cpf", inf.Tomador.CPF)
                                        : new XElement(ns + "Cnpj", inf.Tomador.CNPJ)
                                )
                            ),
                            new XElement(ns + "RazaoSocial", inf.Tomador.Nome),
                            new XElement(ns + "Endereco",
                                new XElement(ns + "Endereco", inf.Tomador.Endereco.Logradouro),
                                new XElement(ns + "Numero", inf.Tomador.Endereco.Numero),
                                new XElement(ns + "Bairro", inf.Tomador.Endereco.Bairro),
                                new XElement(ns + "CodigoMunicipio", tomadorMunicipio.CodMunicipio),
                                new XElement(ns + "Uf", "ES"),
                                new XElement(ns + "Cep", tomadorMunicipio.CEP)
                            )
                        ),

                        new XElement(ns + "OptanteSimplesNacional", inf.Prestador.Regime.OptanteSimplesNacional == 0 ? 2 : 1),
                        new XElement(ns + "IncentivoFiscal", "2")
                    )
                )
            );

            return SerializarXml(new XDocument(root), omitirDeclaracao: false);
        }

        private sealed class StringWriterComEncoding : StringWriter
        {
            private readonly Encoding _encoding;

            public StringWriterComEncoding(Encoding encoding)
            {
                _encoding = encoding;
            }

            public override Encoding Encoding => _encoding;
        }

        private static string MontarEnvelopeSoap(string dadosXml)
        {
            XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace nfse = "http://nfse.abrasf.org.br";

            var cabecalhoXml =
                "<cabecalho versao=\"2.04\" xmlns=\"http://www.abrasf.org.br/nfse.xsd\">" +
                "<versaoDados>2.04</versaoDados>" +
                "</cabecalho>";

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(soap + "Envelope",
                    new XAttribute(XNamespace.Xmlns + "soapenv", soap),
                    new XAttribute(XNamespace.Xmlns + "nfse", nfse),

                    new XElement(soap + "Header"),
                    new XElement(soap + "Body",
                        new XElement(nfse + "GerarNfse",
                            new XElement(nfse + "GerarNfseRequest",
                                new XElement("nfseCabecMsg", new XCData(cabecalhoXml)),
                                new XElement("nfseDadosMsg", new XCData(dadosXml))
                            )
                        )
                    )
                )
            );

            return SerializarXml(doc);
        }

        private NFSeResponse<RespostaEnvioDps> ParseRespostaEnvioSoap(string xmlEnvio, string soapEnvio, string soapRetorno, bool httpSucesso)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            try
            {
                var soapDoc = XDocument.Parse(soapRetorno);

                XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";
                XNamespace abrasfNs = "http://www.abrasf.org.br/nfse.xsd";

                RespostaEnvioDps resultado = new RespostaEnvioDps();

                // garante lista sempre inicializada
                resultado.Erros ??= new List<MensagemProcessamento>();

                // 1) SOAP Fault
                var fault = soapDoc.Descendants(soapNs + "Fault").FirstOrDefault();
                if (fault != null)
                {
                    var faultString =
                        fault.Elements().FirstOrDefault(x => x.Name.LocalName == "faultstring")?.Value
                        ?? "Falha SOAP não identificada.";

                    resultado.Erros.Add(new MensagemProcessamento
                    {
                        Codigo = "SOAP_FAULT",
                        Mensagem = faultString,
                        Descricao = faultString,
                        Complemento = null,
                        Parametros = new List<string>()
                    });

                    return NFSeResponse<RespostaEnvioDps>.Create(
                        xmlEnvio,
                        soapEnvio,
                        JsonSerializer.Serialize(resultado, jsonOptions),
                        false,
                        jsonOptions);
                }

                // 2) outputXML
                var outputXmlNode = soapDoc
                    .Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "outputXML");

                if (outputXmlNode == null || string.IsNullOrWhiteSpace(outputXmlNode.Value))
                {
                    resultado.Erros.Add(new MensagemProcessamento
                    {
                        Codigo = "SEM_OUTPUTXML",
                        Mensagem = "A resposta SOAP não contém o nó outputXML.",
                        Descricao = "A resposta SOAP não contém o nó outputXML.",
                        Complemento = null,
                        Parametros = new List<string>()
                    });

                    return NFSeResponse<RespostaEnvioDps>.Create(
                        xmlEnvio,
                        soapEnvio,
                        JsonSerializer.Serialize(resultado, jsonOptions),
                        false,
                        jsonOptions);
                }

                var xmlInterno = outputXmlNode.Value.Trim();
                var retornoDoc = XDocument.Parse(xmlInterno);

                // 3) erros de negócio
                var mensagens = retornoDoc
                    .Descendants(abrasfNs + "ListaMensagemRetorno")
                    .Descendants(abrasfNs + "MensagemRetorno")
                    .Select(x => new MensagemProcessamento
                    {
                        Codigo = x.Element(abrasfNs + "Codigo")?.Value,
                        Mensagem = x.Element(abrasfNs + "Mensagem")?.Value,
                        Descricao = x.Element(abrasfNs + "Correcao")?.Value,
                        Complemento = null,
                        Parametros = new List<string>()
                    })
                    .ToList();

                if (mensagens.Any())
                {
                    resultado.Erros = mensagens;

                    return NFSeResponse<RespostaEnvioDps>.Create(
                        xmlEnvio,
                        soapEnvio,
                        JsonSerializer.Serialize(resultado, jsonOptions),
                        false,
                        jsonOptions);
                }

                // 4) sucesso
                var compNfse = retornoDoc
                    .Descendants(abrasfNs + "CompNfse")
                    .FirstOrDefault();

                var nfse = retornoDoc
                    .Descendants(abrasfNs + "Nfse")
                    .FirstOrDefault();

                var xmlNfse = compNfse?.ToString(SaveOptions.DisableFormatting)
                           ?? nfse?.ToString(SaveOptions.DisableFormatting);

                if (string.IsNullOrWhiteSpace(xmlNfse))
                {
                    resultado.Erros.Add(new MensagemProcessamento
                    {
                        Codigo = "SEM_NFSE",
                        Mensagem = "Não foi possível localizar CompNfse/Nfse no XML interno de retorno.",
                        Descricao = "Não foi possível localizar CompNfse/Nfse no XML interno de retorno.",
                        Complemento = null,
                        Parametros = new List<string>()
                    });

                    return NFSeResponse<RespostaEnvioDps>.Create(
                        xmlEnvio,
                        soapEnvio,
                        JsonSerializer.Serialize(resultado, jsonOptions),
                        false,
                        jsonOptions);
                }

                var idDps = retornoDoc
                    .Descendants(abrasfNs + "InfDeclaracaoPrestacaoServico")
                    .Attributes("Id")
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? string.Empty;

                var chaveAcesso = retornoDoc
                    .Descendants(abrasfNs + "InfNfse")
                    .Elements(abrasfNs + "CodigoVerificacao")
                    .Select(x => x.Value)
                    .FirstOrDefault() ?? string.Empty;

                var dataEmissaoTexto = retornoDoc
                    .Descendants(abrasfNs + "InfNfse")
                    .Elements(abrasfNs + "DataEmissao")
                    .Select(x => x.Value)
                    .FirstOrDefault();

                if (DateTimeOffset.TryParse(dataEmissaoTexto, out var dataProc))
                    resultado.DataHoraProcessamento = dataProc;

                resultado.IdDps = idDps;
                resultado.ChaveAcesso = chaveAcesso;
                resultado.XmlNFSe = xmlNfse;
                resultado.Erros = new List<MensagemProcessamento>();

                return NFSeResponse<RespostaEnvioDps>.Create(
                    xmlEnvio,
                    soapEnvio,
                    JsonSerializer.Serialize(resultado, jsonOptions),
                    httpSucesso,
                    jsonOptions);
            }
            catch (Exception ex)
            {
                var resultado = new RespostaEnvioDps
                {
                    Erros = new List<MensagemProcessamento>
            {
                new MensagemProcessamento
                {
                    Codigo = "ERRO_PARSE",
                    Mensagem = $"Erro ao interpretar retorno SOAP: {ex.Message}",
                    Descricao = ex.Message,
                    Complemento = null,
                    Parametros = new List<string>()
                }
            }
                };

                return NFSeResponse<RespostaEnvioDps>.Create(
                    xmlEnvio,
                    soapEnvio,
                    JsonSerializer.Serialize(resultado),
                    false,
                    jsonOptions);
            }
        }

        protected async Task<HttpResponseMessage> SendSoap(HttpContent? content, HttpMethod method, string url)
        {
            var handler = new HttpClientHandler
            {
                SslProtocols = (System.Security.Authentication.SslProtocols)Configuracao.WebServices.Protocolos
            };

            //handler.ClientCertificates.Add(Configuracao.Certificados.ObterCertificado());

            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(method, url);

            var assemblyName = GetType().Assembly.GetName();
            request.Headers.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("OpenAC.Net.NFSe.Nacional", assemblyName.Version!.ToString()));
            request.Headers.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("(+https://github.com/OpenAC-Net/OpenAC.Net.NFSe.Nacional)"));

            request.Headers.TryAddWithoutValidation("SOAPAction", "http://nfse.abrasf.org.br/GerarNfse");
            request.Content = content;

            return await client.SendAsync(request);
        }

        private static string SerializarXml(XDocument doc, bool omitirDeclaracao = false)
        {
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = omitirDeclaracao,
                Indent = true,
                IndentChars = "\t",
                NewLineChars = Environment.NewLine,
                NewLineHandling = NewLineHandling.Replace,
                Encoding = new UTF8Encoding(false)
            };

            using var sw = new StringWriterComEncoding(new UTF8Encoding(false));

            using (var writer = XmlWriter.Create(sw, settings))
            {
                doc.WriteTo(writer);
                writer.Flush();
            }

            return sw.ToString();
        }

        #endregion Métodos Auxiliares

        #endregion Métodos
    }
}
