namespace Nouzuru
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class DebuggerSettings
    {
        #region Properties

        public bool PauseOnSingleStep { get; set; }

        public bool PauseOnAccessViolation { get; set; }

        public bool PauseOnArrayBoundsExceeded { get; set; }

        public bool PauseOnBreakpoint { get; set; }

        public bool PauseOnDatatypeMisalignment { get; set; }

        public bool PauseOnFltDenormalOperand { get; set; }

        public bool PauseOnFltDivideByZero { get; set; }

        public bool PauseOnFltInexactResult { get; set; }

        public bool PauseOnFltInvalidOperation { get; set; }

        public bool PauseOnFltOverflow { get; set; }

        public bool PauseOnFltStackCheck { get; set; }

        public bool PauseOnFltUnderflow { get; set; }

        public bool PauseOnGuardPage { get; set; }

        public bool PauseOnIllegalInstruction { get; set; }

        public bool PauseOnInPageError { get; set; }

        public bool PauseOnIntDivideByZero { get; set; }

        public bool PauseOnIntOVerflow { get; set; }

        public bool PauseOnInvalidDisposition { get; set; }

        public bool PauseOnNoncontinuableException { get; set; }

        public bool PauseOnPrivInstruction { get; set; }

        public bool PauseOnStackOverflow { get; set; }

        public bool PauseOnUnhandledDebugException { get; set; }

        /// <summary>
        /// Gets or sets other flags that determine whether or not the debugger will pause on
        /// exploitable exceptions.
        /// </summary>
        public bool PauseOnExploitableException
        {
            get
            {
                return
                    this.PauseOnGuardPage &&
                    this.PauseOnAccessViolation &&
                    this.PauseOnIntOVerflow &&
                    this.PauseOnStackOverflow;
            }

            set
            {
                this.PauseOnGuardPage = true;
                this.PauseOnAccessViolation = true;
                this.PauseOnIntOVerflow = true;
                this.PauseOnStackOverflow = true;
            }
        }

        #endregion
    }
}
