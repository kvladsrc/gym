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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  int max_len = 0;
  int cur_len = 0;
  int prev = -1;
  while (n--) {
    int buf;
    std::cin >> buf;

    if (buf > prev) {
      cur_len++;
    } else {
      if (cur_len > max_len) max_len = cur_len;
      cur_len = 1;
    }

    prev = buf;
  }

  if (cur_len > max_len) max_len = cur_len;
  std::cout << max_len << "\n";

  return 0;
}
