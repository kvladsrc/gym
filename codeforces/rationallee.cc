#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    vector<int> nums(n);
    for (auto &i : nums) {
      std::cin >> i;
    }

    vector<int> nums_for_friend(k);
    for (auto &i : nums_for_friend) {
      std::cin >> i;
    }

    sort(nums.begin(), nums.end());
    sort(nums_for_friend.begin(), nums_for_friend.end(), std::greater<int>());

    int64_t res = 0;

    // Special case.
    while (nums_for_friend.back() == 1) {
      res += 2 * nums.back();
      nums.pop_back();
      nums_for_friend.pop_back();
    }

    int minimal_idx = 0;
    int maximal_idx = nums.size() - 1;

    for (int w : nums_for_friend) {
      res += nums[minimal_idx++] + nums[maximal_idx--];
      minimal_idx += w - 2;
    }

    std::cout << res << "\n";
  }

  return 0;
}
