#ifndef CPP_WARMUP_QUICK_SORT_H_
#define CPP_WARMUP_QUICK_SORT_H_

#include <vector>

namespace warmup {

int partition(std::vector<int> &a, int l, int r);
void quick_sort(std::vector<int> &a, int l, int r);

}  // namespace warmup

#endif  // CPP_WARMUP_QUICK_SORT_H_
