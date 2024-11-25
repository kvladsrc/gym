#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int solve(int n) {
  if (n % 2) {
    return n / 2;
  } else {
    return n / 2 - 1;
  }
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> candies(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> candies[i];
  }

  for (int c : candies) {
    std::cout << solve(c) << std::endl;
  }

  return 0;
}
