#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  int ones = 0, zeroes = 0;

  for (int i = 0; i < n; ++i) {
    char c;
    std::cin >> c;
    if (c == '0') {
      zeroes++;
    } else {
      ones++;
    }
  }

  std::cout << n - (min(ones, zeroes) * 2) << "\n";

  return 0;
}
