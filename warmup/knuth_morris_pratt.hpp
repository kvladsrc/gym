#ifndef CPP_WARMUP_KNUTH_MORRIS_PRATT_H_
#define CPP_WARMUP_KNUTH_MORRIS_PRATT_H_

#include <cstddef>
#include <string>
#include <vector>

namespace warmup {

void compute_border_lengths(const std::string& pattern,
                            std::vector<std::size_t>& borders);
bool kmp(const std::string& s, const std::string& pattern);

}  // namespace warmup

#endif  // CPP_WARMUP_KNUTH_MORRIS_PRATT_H_
