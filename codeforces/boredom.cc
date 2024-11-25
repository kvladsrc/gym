#include <algorithm>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

// O(n)
long long optimal_gain(vector<long long> &nums) {
  long long max_num = 0;
  for (long long num : nums) {
    if (num > max_num) max_num = num;
  }
  vector<long long> elements(max_num + 1);
  for (long long num : nums) {
    elements[num]++;
  }

  vector<long long> dp(max_num + 1);
  dp[0] = 0;
  dp[1] = elements[1];
  for (long long i = 2; i <= max_num; ++i) {
    dp[i] = max(dp[i - 1], dp[i - 2] + elements[i] * i);
  }
  return dp[max_num];
}

int main() {
  long long n;
  std::cin >> n;
  vector<long long> nums(n);
  for (long long i = 0; i < n; ++i) {
    std::cin >> nums[i];
  }
  std::cout << optimal_gain(nums) << "\n";
}
