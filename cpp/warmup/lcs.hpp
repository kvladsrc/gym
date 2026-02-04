#ifndef CPP_WARMUP_LCS_H_
#define CPP_WARMUP_LCS_H_

#include <vector>

namespace warmup {

int lcs_dp(const std::vector<int>& a, const std::vector<int>& b);
int lcs_rec(const std::vector<int>& a, const std::vector<int>& b);

}  // namespace warmup

#endif  // CPP_WARMUP_LCS_H_
