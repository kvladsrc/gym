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
  string in;
  std::cin >> in;

  vector<int> ab;
  vector<int> ba;

  for (size_t i = 0; i + 1 < in.size(); ++i) {
    if (in[i] == 'A' && in[i + 1] == 'B') ab.push_back(i);
    if (in[i] == 'B' && in[i + 1] == 'A') ba.push_back(i);
  }

  if (ab.empty() || ba.empty()) {
    std::cout << "NO"
              << "\n";
  } else {
    if (ab[ab.size() - 1] - ba[0] > 1 || ba[ba.size() - 1] - ab[0] > 1) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
