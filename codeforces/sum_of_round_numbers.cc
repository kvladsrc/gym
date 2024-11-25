#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

void print_round_nums(int n) {
  int power = 10;
  int c = 0;
  string res;
  while (n) {
    if (n % power) {
      res += std::to_string(n % power) + " ";
      c++;
    }
    n -= n % power;
    power *= 10;
  }
  std::cout << c << "\n";
  std::cout << res << "\n";
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> nums(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> nums[i];
  }

  for (int n : nums) {
    print_round_nums(n);
  }

  return 0;
}
