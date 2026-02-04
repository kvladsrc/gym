#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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

  if (!n) {
    std::cout << 1 << "\n";
    return 0;
  }

  n = n % 4;
  if (!n) n = 4;

  int power = 0;
  int res = 1;
  while (power < n) {
    power++;
    res = (res * 8) % 10;
  }
  std::cout << res << "\n";

  return 0;
}
