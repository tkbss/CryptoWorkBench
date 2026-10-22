#define _DARWIN_C_SOURCE 1
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stddef.h>
#include <sys/param.h>
#include <sys/types.h>

// Only fixed-width, non-variadic contracts cross the managed/native boundary.
// The SDK prototypes and compiler implement Apple's architecture-specific varargs ABI.
#define CWE_EXPORT __attribute__((visibility("default")))

CWE_EXPORT int cwe_openat(int fd, const char *path, int flags, uint32_t mode)
{
    return openat(fd, path, flags, (mode_t)mode);
}

CWE_EXPORT int cwe_getpath(int fd, char *path, int capacity)
{
    // F_GETPATH does not accept a buffer length. Enforce its SDK-defined bound here.
    if (path == NULL || capacity < MAXPATHLEN) {
        errno = EINVAL;
        return -1;
    }
    return fcntl(fd, F_GETPATH, path);
}
