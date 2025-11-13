#include <algorithm>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  vector<int> nums(4);
  std::cin >> nums[0] >> nums[1] >> nums[2] >> nums[3];
  sort(nums.begin(), nums.end());

  // Lets assume the followng order, x4 is only matter to be largest
  // num.
  //
  // 0 = a + b
  // 1 = a + c
  // 2 = b + c
  // 3 = a + b + c

  int a, b, c;
  a = nums[3] - nums[2];
  b = nums[0] - a;
  c = nums[1] - a;

  std::cout << a << " ";
  std::cout << b << " ";
  std::cout << c << "\n";

  return 0;
}
