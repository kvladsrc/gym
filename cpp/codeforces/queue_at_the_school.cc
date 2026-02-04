#include <cstddef>
#include <iostream>
#include <string>

using std::string;

void tick(string& q) {
  if (q.size() <= 1) return;

  for (size_t i = 1; i < q.size(); ++i) {
    if (q[i] == 'G' && q[i - 1] == 'B') {
      q[i - 1] = 'G';
      q[i++] = 'B';
    }
  }
}

int main(int argc, char* argv[]) {
  int n, t;
  std::cin >> n >> t;
  string q;
  std::cin >> q;
  while (t) {
    tick(q);
    t--;
  }

  std::cout << q << "\n";

  return 0;
}
