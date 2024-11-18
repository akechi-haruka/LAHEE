/**
 * Represents a time interval (i.e. the difference between two times or an amount of time)
 */
class TimeSpan {
    /**
     * Creates a new TimeSpan with the given values for each portion
     * @param {Date|TimeSpan|number} [t=0] Date, TimeSpan, or number of milliseconds 
     * @param {number} [s=0] Seconds
     * @param {number} [m=0] Minutes
     * @param {number} [h=0] Hours
     * @param {number} [d=0] Days
     */
    constructor(t = 0, s = 0, m = 0, h = 0, d = 0) {
        this.totalMilliseconds = (
            t +
            s * 1000 +
            m * 1000 * 60 +
            h * 1000 * 60 * 60 +
            d * 1000 * 60 * 60 * 24
        );
    }

    /**
     * TimeSpan representing no time
     * @type {TimeSpan}
     */
    static get zero() {
        return new TimeSpan();
    }

    /**
     * Creates a TimeSpan representing the given number of milliseconds
     * @param {number} t A number of milliseconds
     * @returns {TimeSpan}
     */
    static fromMilliseconds(t) {
        return new TimeSpan(t);
    }

    /**
     * Creates a TimeSpan representing the given number of seconds
     * @param {number} t A number of seconds
     * @returns {TimeSpan}
     */
    static fromSeconds(s) {
        return new TimeSpan(s * 1000);
    }

    /**
     * Creates a TimeSpan representing the given number of minutes
     * @param {number} t A number of minutes
     * @returns {TimeSpan}
     */
    static fromMinutes(m) {
        return new TimeSpan(m * 1000 * 60);
    }

    /**
     * Creates a TimeSpan representing the given number of hours
     * @param {number} t A number of hours
     * @returns {TimeSpan}
     */
    static fromHours(h) {
        return new TimeSpan(h * 1000 * 60 * 60);
    }

    /**
     * Creates a TimeSpan representing the given number of days
     * @param {number} t A number of days
     * @returns {TimeSpan}
     */
    static fromDays(d) {
        return new TimeSpan(d * 1000 * 60 * 60 * 24);
    }

    /**
     * The total number of full and partial seconds in the TimeSpan
     * @returns {number}
     */
    get totalSeconds() {
        return this.totalMilliseconds / 1000;
    }

    /**
     * The total number of full and partial minutes in the TimeSpan
     * @returns {number}
     */
    get totalMinutes() {
        return this.totalSeconds / 60;
    }

    /**
     * The total number of full and partial hours in the TimeSpan
     * @returns {number}
     */
    get totalHours() {
        return this.totalMinutes / 60;
    }

    /**
     * The total number of full and partial days in the TimeSpan
     * @returns {number}
     */
    get totalDays() {
        return this.totalHours / 24;
    }

    /**
     * The milliseconds component of the TimeSpan
     * @returns {number} An integer number of milliseconds from 0 to 999
     */
    get milliseconds() {
        return Math.floor(this.totalMilliseconds % 1000);
    }

    /**
     * The seconds component of the TimeSpan
     * @returns {number} An integer number of seconds from 0 to 59
     */
    get seconds() {
        return Math.floor(this.totalSeconds % 60);
    }

    /**
     * The minutes component of the TimeSpan
     * @returns {number} An integer number of minutes from 0 to 59
     */
    get minutes() {
        return Math.floor(this.totalMinutes % 60);
    }

    /**
     * The hours component of the TimeSpan
     * @returns {number} An integer number of hours from 0 to 24
     */
    get hours() {
        return Math.floor(this.totalHours % 24);
    }

    /**
     * The days component of the TimeSpan
     * @returns {number} An integer number of days greater than or equal to 0
     */
    get days() {
        return Math.floor(this.totalDays);
    }

    /**
     * Returns a short string representation of the TimeSpan
     * @example
     * let t = new TimeSpan(100)
     * console.log(t.toShortString()) // "100 milliseconds"
     * t = t.add(900)
     * console.log(t.toShortString()) // "1 second"
     * t = TimeSpan.fromHours(3.4)
     * console.log(t.toShortString()) // "3 hours"
     * @returns {string}
     */
    toShortString() {
        let unit =
            Math.abs(this.totalDays) >= 1
                ? `day${Math.floor(Math.abs(this.totalDays)) == 1 ? '' : 's'}`
                : Math.abs(this.totalHours) >= 1
                    ? `hour${Math.floor(Math.abs(this.totalHours)) == 1 ? '' : 's'}`
                    : Math.abs(this.totalMinutes) >= 1
                        ? `minute${Math.floor(Math.abs(this.totalMinutes)) == 1 ? '' : 's'}`
                        : Math.abs(this.totalSeconds) >= 1
                            ? `second${Math.floor(Math.abs(this.totalSeconds)) == 1 ? '' : 's'}`
                            : `millisecond${Math.floor(Math.abs(this.totalMilliseconds)) == 1 ? '' : 's'}`
        return Math.floor({
            day: this.totalDays,
            hour: this.totalHours,
            minute: this.totalMinutes,
            second: this.totalSeconds,
            millisecond: this.totalMilliseconds,
        }[unit.endsWith('s') ? unit.slice(0, -1) : unit]) + ' ' + unit;
    }

    /**
     * Returns a long string representation of the TimeSpan
     * @example
     * let t = TimeSpan.fromHours(3.4)
     * console.log(t.toLongString()) // "3 hours 24 minutes"
     * console.log(t.toLongString(true)) // "0 days 3 hours 24 minutes 0 seconds"
     * t = t.add(3500)
     * console.log(t.toLongString()) // "3 hours 24 minutes 3 seconds"
     * console.log(t.toLongString(true)) // "0 days 3 hours 24 minutes 3 seconds"
     * console.log(t.toLongString(false, true)) // "3 hours 24 minutes 3 seconds 500 milliseconds"
     * @param {boolean} [includeZeroValues = false] Whether or not to include zero values in the string
     * @param {boolean} [includeMilliseconds = false] Whether or not to include milliseconds in the string
     * @returns {string}
     */
    toLongString(includeZeroValues = false, includeMilliseconds = false) {
        if (this.totalMilliseconds == 0) {
            return includeMilliseconds ? "0 milliseconds" : "0 seconds";
        }
        let string = "";
        if (includeZeroValues || this.days) string += `${this.days} day${this.days === 1 ? '' : 's'} `;
        if (includeZeroValues || this.hours) string += `${this.hours} hour${this.hours === 1 ? '' : 's'} `;
        if (includeZeroValues || this.minutes) string += `${this.minutes} minute${this.minutes === 1 ? '' : 's'} `;
        if (includeZeroValues || this.seconds) string += `${this.seconds} second${this.seconds === 1 ? '' : 's'} `;
        if (includeMilliseconds && (includeZeroValues || this.milliseconds)) string += `${this.milliseconds} millisecond${this.milliseconds === 1 ? '' : 's'}`;
        return string.trim();
    }

    /**
     * Returns a string representation of the TimeSpan
     * @example
     * let t = new TimeSpan(TimeSpan.fromHours(3) + TimeSpan.fromMinutes(24) + TimeSpan.fromSeconds(32))
     * console.log(t.toString()) // "03:24:32"
     * t = t.add(500)
     * console.log(t.toString()) // "03:24:32.500"
     * t = t.add(TimeSpan.fromDays(1))
     * console.log(t.toString()) // "1.03:24:32.500"
     */
    toString() {
        let pad = (n, w = 2, z = '0') => n.toString().padStart(w, z);
        return `${this.totalMilliseconds < 0 ? '-' : ''}${Math.abs(this.days) > 0 ? `${Math.abs(this.days)}.` : ""}${pad(Math.abs(this.hours))}:${pad(Math.abs(this.minutes))}:${pad(Math.abs(this.seconds))}${Math.abs(this.milliseconds) > 0 ? `.${Math.abs(this.milliseconds).toString().padEnd(3, '0')}` : ""}`;
    }

    /**
     * Returns the number of milliseconds in the TimeSpan
     * @returns {number}
     */
    valueOf() {
        return this.totalMilliseconds;
    }

    /**
     * Returns a new TimeSpan representing the value of
     * the current TimeSpan plus the value of another TimeSpan
     * or individual values for each interval
     * @param {TimeSpan|number} t TimeSpan or milliseconds
     * @param {number} [s=0] Seconds
     * @param {number} [m=0] Minutes
     * @param {number} [h=0] Hours
     * @param {number} [d=0] Days
     */
    add(t, s = 0, m = 0, h = 0, d = 0) {
        let ms = t;
        if (s) ms += s * 1000;
        if (m) ms += m * 60 * 1000;
        if (h) ms += h * 60 * 60 * 1000;
        if (d) ms += d * 24 * 60 * 60 * 1000;
        return new TimeSpan(this.totalMilliseconds + ms);
    }

    /**
     * Returns a new TimeSpan representing the value of
     * the current TimeSpan minus the value of another TimeSpan
     * or individual values for each interval
     * @param {TimeSpan|number} t TimeSpan or milliseconds
     * @param {number} [s=0] Seconds
     * @param {number} [m=0] Minutes
     * @param {number} [h=0] Hours
     * @param {number} [d=0] Days
     */
    subtract(t, s = 0, m = 0, h = 0, d = 0) {
        return this.add.apply(this, Array.from(arguments).map(x => -x));
    }

    /**
     * Returns a value indicating whether this instance is equal to a specified TimeSpan object
     * @param {TimeSpan} t Another TimeSpan object
     */
    equals(t) {
        return this.totalMilliseconds === t.totalMilliseconds;
    }

    /**
     * Compares this TimeSpan to another TimeSpan, returning an integer that indicates
     * whether this instance is less than, equal to, or greater than the specified TimeSpan
     * @param {TimeSpan} t Another TimeSpan object
     * @returns {Number} A positive number if `this > t`, 0 if `this == t`, a negative number if `this < t`
     */
    compareTo(t) {
        return this.totalMilliseconds - t.totalMilliseconds;
    }

    /**
     * Returns a new TimeSpan equal to the absolute value of this TimeSpan
     * @returns {TimeSpan}
     */
    duration() {
        return new TimeSpan(Math.abs(this.totalMilliseconds));
    }

    /**
     * Returns a new TimeSpan equal to the negated value of this TimeSpan
     * @returns {TimeSpan}
     */
    negate() {
        return new TimeSpan(-this.totalMilliseconds);
    }

    /**
     * @private
     * Regular expression matching a valid TimeSpan string
     */
    static get _regex() {
        return /(?:(\d+)\.)?(0[0-9]|1[0-9]|2[0-4]):([0-5][0-9]):([0-5][0-9])(?:\.([0-9]{3}))?/;
    }

    /**
     * Parses strings in the format returned from `toString`
     * into TimeSpan objects
     * @param {string} s A string representation of a TimeSpan
     * @returns {TimeSpan}
     */
    static parse(s) {
        if (typeof s !== "string") throw new TypeError("Parameter s must be of type string");
        let match = s.match(TimeSpan._regex);
        if (match) {
            let n = [undefined];
            n.push(...Array.from(match).slice(1).map(x => x ? Number.parseInt(x) : x));
            return new TimeSpan(n[5], n[4], n[3], n[2], n[1]);
        }
        throw new Error(`Input string was not in the correct format: "${s}"`);
    }
}