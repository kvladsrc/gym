#ifndef CPP_WARMUP_PARTITION_H_
#define CPP_WARMUP_PARTITION_H_

#include <vector>

namespace warmup {

// Partitions the vector using the last element as the pivot.
// This is Lomuto partition scheme.
int partition_to_right(std::vector<int>& a);

// Partitions the vector using the last element as the pivot.
// This is a variant of Hoare partition scheme.
int partition_to_mid(std::vector<int>& a);

}  // namespace warmup

#endif  // CPP_WARMUP_PARTITION_H_
