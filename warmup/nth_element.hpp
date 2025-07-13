#ifndef CPP_WARMUP_NTH_ELEMENT_H_
#define CPP_WARMUP_NTH_ELEMENT_H_

#include <optional>
#include <vector>

namespace warmup {

// Finds the median of the elements in the vector using the nth_element
// algorithm. Returns std::nullopt if the vector is empty.
std::optional<int> nth_element_median(std::vector<int>& a);

}  // namespace warmup

#endif  // CPP_WARMUP_NTH_ELEMENT_H_
