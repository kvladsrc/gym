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

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  string in;
  std::cin >> in;
  int left_shift = 0, right_shift = 0;
  for (char c : in) {
    if (c == 'L') {
      left_shift++;
    } else {
      right_shift++;
    }
  }
  std::cout << left_shift + right_shift + 1 << "\n";
  return 0;
}
