#include <algorithm>
#include <cstdint>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

// O(n)
int64_t optimal_gain(const vector<int64_t>& nums) {
  int64_t max_num = 0;
  if (!nums.empty()) {
    max_num = *std::max_element(nums.begin(), nums.end());
  }
  vector<int64_t> elements(max_num + 1);
  for (int64_t num : nums) {
    elements[num]++;
  }

  vector<int64_t> dp(max_num + 1);
  dp[0] = 0;
  dp[1] = elements[1];
  for (int64_t i = 2; i <= max_num; ++i) {
    dp[i] = max(dp[i - 1], dp[i - 2] + elements[i] * i);
  }
  return dp[max_num];
}

int main() {
  int64_t n;
  std::cin >> n;
  vector<int64_t> nums(n);
  for (int64_t i = 0; i < n; ++i) {
    std::cin >> nums[i];
  }
  std::cout << optimal_gain(nums) << "\n";
}
