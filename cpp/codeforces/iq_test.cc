#include <algorithm>
#include <cstddef>
#include <iostream>
#include <numeric>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int solve(const vector<int>& nums) {
  int odds = 0;
  odds = std::accumulate(nums.begin(), nums.begin() + 3, 0,
                         [](int acc, int n) { return acc + n % 2; });

  for (size_t i = 0; i < nums.size(); ++i) {
    if (odds >= 2 && !(nums[i] % 2)) return i + 1;
    if (odds <= 1 && nums[i] % 2) return i + 1;
  }

  return 0;
}

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> nums(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> nums[i];
  }
  std::cout << solve(nums) << "\n";

  return 0;
}
