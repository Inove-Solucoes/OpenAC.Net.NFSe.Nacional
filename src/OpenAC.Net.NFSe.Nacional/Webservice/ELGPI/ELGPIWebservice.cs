using OpenAC.Net.Core.Logging;
using OpenAC.Net.NFSe.Nacional.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Common.Types;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAC.Net.NFSe.Nacional.Webservice.ELGPI
{
    /// <summary>
    /// Provedor para o Webservice ELGPI.
    /// Este é o provedor dos municípios de Domingos Martins, Marilândia e Mantenópolis
    /// Autor: Lucas Giovani de Paula Salgado
    /// Contato: lucas@salgado.dev
    /// Data: 13/03/2026
    /// </summary>
    public class ELGPIWebservice : NFSeWebserviceBase
    {
        #region Constructor
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="ELGPIWebservice"/>
        /// </summary>
        /// <param name="configuracaoNFSe">Configuração da NFSe.</param>
        /// <param name="serviceInfo">Informações do serviço</param>
        public ELGPIWebservice(ConfiguracaoNFSe configuracaoNFSe, NFSeServiceInfo serviceInfo)
            : base(configuracaoNFSe, serviceInfo)
        { Console.WriteLine("Provider Domingos Martins Caregado."); }

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
            if (dps.Informacoes.Servico.InformacoesComplementares == null || string.IsNullOrEmpty(dps.Informacoes.Servico.InformacoesComplementares.Informacoes))
                throw new Exception("O Token de acesso não foi informador nas configurações");

            var token = dps.Informacoes.Servico.InformacoesComplementares.Informacoes;
            dps.Informacoes.Servico.InformacoesComplementares.Informacoes = string.Empty;

            dps.Informacoes.Servico.Informacoes.CodInterno = dps.Informacoes.Servico.Informacoes.CodTributacaoMunicipio;
            dps.Informacoes.Servico.Informacoes.CodTributacaoMunicipio = string.Empty;
            dps.Informacoes.Valores.Tributos.Municipal.Aliquota = dps.Informacoes.Valores.Tributos.Total.PorcentagemTotal.TotalMunicipal;
            
            dps.Assinar(Configuracao);
            
            ValidarSchema(SchemaNFSe.DPS, dps.Xml, dps.Versao);

            var documento = dps.Informacoes.Prestador.CPF ?? dps.Informacoes.Prestador.CNPJ;

            GravarDpsEmDisco(dps.Xml, $"{dps.Informacoes.NumeroDps:000000}_dps.xml",
                documento, dps.Informacoes.DhEmissao.DateTime);

            var envio = new DpsEnvio
            {
                XmlDps = dps.Xml
            };

            var content = JsonContent.Create(envio);
            var strEnvio = await content.ReadAsStringAsync();

            this.Log().Debug($"Webservice ELG GPI: [Enviar][Envio] - {strEnvio}");

            GravarArquivoEmDisco(strEnvio, $"Enviar-{dps.Informacoes.NumeroDps:000000}-env.json", documento);

            var url = ServiceInfo[Configuracao.WebServices.Ambiente][TipoUrl.Enviar];
            var httpResponse = await SendAsync(content, HttpMethod.Post, $"{url}?token={token}");

            var strResponse = await httpResponse.Content.ReadAsStringAsync();

            this.Log().Debug($"Webservice ELG GPI: [Enviar][Resposta] - {strResponse}");

            strResponse = strResponse
                .Replace("\"tipoAmbiente\":\"HOMOLOGACAO\"", "\"tipoAmbiente\":2")
                .Replace("\"tipoAmbiente\":\"PRODUCAO\"", "\"tipoAmbiente\":1");

            strResponse = strResponse.Replace("\"mensagem\":{}", "\"mensagem\":\"Falha ao enviar\"");

            GravarArquivoEmDisco(strResponse, $"Enviar-{dps.Informacoes.NumeroDps:000000}-resp.json", documento);
            var retorno = NFSeResponse<RespostaEnvioDps>.Create(dps.Xml, await JsonContent.Create(envio.XmlDps).ReadAsStringAsync(), strResponse, httpResponse.IsSuccessStatusCode, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            if (retorno.Sucesso)
                GravarNFSeEmDisco(retorno.Resultado.XmlNFSe, $"{dps.Informacoes.NumeroDps:000000}_nfse.xml", documento, dps.Informacoes.DhEmissao.DateTime);

            return retorno;
        }

        #endregion NFS-e

        #endregion Métodos
    }
}
