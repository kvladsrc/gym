#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
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
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  string s;
  std::cin >> s;

  int ones = 0, zeroes = 0;
  for (char c : s) {
    if (c == 'n') ones++;
    if (c == 'z') zeroes++;
  }

  while (ones || zeroes) {
    if (ones) {
      std::cout << 1 << " ";
      ones--;
      continue;
    }

    if (zeroes) {
      std::cout << 0 << " ";
      zeroes--;
    }
  }
  std::cout << "\n";

  return 0;
}
