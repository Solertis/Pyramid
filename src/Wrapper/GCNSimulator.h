#ifndef _GCN_SIMULATOR_H_
#define _GCN_SIMULATOR_H_

namespace GCN{
    
    class Instruction;

namespace Simulator{

    struct Settings
    {
        size_t nWaveIssueRate;      ///< Issue one wave every N clocks
        size_t nWavesToExecute;     ///< Execute this many waves before stopping
        size_t nMaxWavesPerSIMD;    ///< Occupancy limit per SIMD
        size_t nExportCost;         ///< How long an export should take
                                    ///<  This is presumed to be a function of the number of CUs to a slice

    };

    struct Results
    {
        ///< Count the number of clocks where at least one wave was stalled on a given instruction
        ///<  Indexed by the InstructionID field in the 'SimOp'
        ///< Caller is responsible for zeroing this
        size_t* pInstructionStallCounts; 

        size_t nCycles;
        size_t nSALUBusy;
        size_t nVALUBusy[4];
        size_t nVMemBusy;
        size_t nExpBusy;
        size_t nSMemBusy;

        size_t nVMemIssued;
        size_t nVALUIssued;
        size_t nScalarIssued;
        size_t nExpIssued;

        size_t nStallCycles[4];    ///< Number of times each SIMD was stalled
        size_t nStallWaves[4];     ///< Incremented once per stalled wave on each stall
    };


    enum Format
    {
        FMT_R8,
        FMT_RG8,
        FMT_RGBA8,
        FMT_R16,
        FMT_RG16,
        FMT_RGBA16,
        FMT_R16F,
        FMT_RG16F,
        FMT_RGBA16F,
        FMT_R32F,
        FMT_RG32F,
        FMT_RGBA32F,
        FMT_BC1,
        FMT_BC2,
        FMT_BC3,
        FMT_BC4,
        FMT_BC5,
        FMT_BC6,
        FMT_BC7
    };

    enum Filter
    {
        FILT_POINT,
        FILT_BILINEAR,
        FILT_TRILINEAR,
        FILT_ANISO_2X,
        FILT_ANISO_4X,
        FILT_ANISO_8X
    };



    struct SimOp
    {
        const GCN::Instruction* pInstruction;
        size_t nInstructionID;  ///< Index of the instruction in the results.pInstructionStallCounts

        // These are used only if 'pInstruction' is a corresponding image instruction
        Format eFormat;
        Filter eFilter;

        ///< Number of times that at least one wave was stalled on this op
        ///< Incremented once per stalled clock/stalled wave
        size_t nStalls; 
    };


    /// Simulate wavefront execution on a single CU
    /// 
    void Simulate( Results& rResults, const Settings& rSettings, SimOp* pOps, size_t nOps );

}};


#endif